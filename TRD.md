```markdown
# 일행로 TRD

## 1. 문서 개요

### 1.1 목적

이 문서는 일행로의 제품 요구사항을 실제 웹 서비스로 구현하기 위한 기술 구조와 구현 방식을 정의한다.

일행로는 사용자가 외출 목적과 상황을 자연어로 입력하면 필요한 업무와 준비물을 제안하고, 사용자의 승인 이후 실행 가능한 외출 계획과 일정 변경에 따른 재계획을 제공한다.

### 1.2 핵심 처리 흐름

```text
사용자 목적·상황 입력
        ↓
사용자 의도와 제약조건 추출
        ↓
필요 업무·준비물 제안
        ↓
사용자 승인·수정
        ↓
외출 계획 생성
        ↓
업무 상태 관리
        ↓
상황 변경 시 재계획
```

### **1.3 MVP 구현 원칙**

- 사용자는 세부 할 일 목록을 모두 직접 작성하지 않아도 된다.
- 자연어 입력을 기본 입력 방식으로 사용한다.
- AI가 제안한 업무는 사용자의 승인 전까지 확정하지 않는다.
- 계획 생성 결과는 구조화된 데이터로 관리한다.
- AI 응답은 가능한 경우 스트리밍으로 표시한다.
- 외부 기관 데이터, 지도 API, 캘린더 연동 없이 동작한다.
- 로그인 없이 핵심 기능을 사용할 수 있다.
- AI 오류가 발생하면 빈 계획으로 대체하지 않고 오류를 표시한다.

## **. 시스템 구성**

```
┌─────────────────────────────────────┐
│          React Web Client           │
│                                     │
│  목적·상황 입력                      │
│  AI 제안 확인·수정                   │
│  외출 계획 타임라인                  │
│  업무 상태 관리                      │
│  자연어 재계획                       │
└──────────────────┬──────────────────┘
                   │ HTTPS / JSON / SSE
                   ▼
┌─────────────────────────────────────┐
│          ASP.NET Core API           │
│                                     │
│  세션 관리                           │
│  계획 생성 API                       │
│  제안 승인 API                       │
│  업무 상태 변경 API                  │
│  재계획 API                          │
│  입력 검증 및 오류 처리              │
└──────────────────┬──────────────────┘
                   │
                   ▼
┌─────────────────────────────────────┐
│       Agent Orchestration Layer      │
│                                     │
│  Context Agent                      │
│  Discovery Agent                    │
│  Task Agent                         │
│  Plan Agent                         │
│  Review Agent                       │
│  Recovery Agent                     │
└──────────────────┬──────────────────┘
                   │
                   ▼
┌─────────────────────────────────────┐
│       GitHub Copilot SDK Layer       │
│                                     │
│  모델 요청                           │
│  대화 컨텍스트                       │
│  구조화 출력                         │
│  스트리밍 응답                       │
└─────────────────────────────────────┘
```

## **3. 기술 스택**

| **영역** | **기술** | **용도** |
| --- | --- | --- |
| 프론트엔드 | React + TypeScript | 반응형 웹 사용자 인터페이스 |
| 스타일링 | CSS 또는 Tailwind CSS | 레이아웃, 카드, 타임라인, 상태 표시 |
| 백엔드 | ASP.NET Core Web API | API와 비즈니스 로직 |
| 모델 연결 | GitHub Copilot SDK | 자연어 처리, 대화, 구조화 출력, 스트리밍 |
| 에이전트 | Microsoft Agent Framework | 에이전트 구성과 오케스트레이션 |
| 로컬 구성 | .NET Aspire | 프론트엔드와 API 실행 및 서비스 구성 |
| 실시간 응답 | Server-Sent Events 또는 HTTP Streaming | AI 진행 상황 및 결과 전달 |
| 임시 상태 저장 | 서버 메모리 또는 브라우저 상태 | 로그인 없는 MVP 세션 관리 |
| 관찰성 | Application Insights | 요청, 오류, 응답 시간 관찰 |
| 배포 | Azure Container Apps | 공개 웹앱과 API 배포 |
| 소스 관리 | GitHub | 코드와 문서 관리 |

### **3.1 MVP에서 사용하지 않는 기능**

- 회원가입 및 로그인
- 영구 데이터베이스 저장
- 지도 또는 경로 최적화 API
- 캘린더 API
- 실시간 공공데이터 API
- 예약·결제·민원 신청 자동화
- 문자·이메일 발송
- 백그라운드 알림
- 푸시 알림

## **4. 프로젝트 구조**

```
ilhaengro/
├─ PRD.md
├─ TRD.md
├─ README.md
├─ src/
│  ├─ Ilhaengro.AppHost/
│  │  └─ Program.cs
│  ├─ Ilhaengro.Api/
│  │  ├─ Controllers/
│  │  ├─ Agents/
│  │  ├─ Services/
│  │  ├─ Models/
│  │  ├─ Infrastructure/
│  │  └─ Program.cs
│  └─ Ilhaengro.Web/
│     ├─ src/
│     │  ├─ components/
│     │  ├─ pages/
│     │  ├─ hooks/
│     │  ├─ services/
│     │  ├─ types/
│     │  ├─ App.tsx
│     │  └─ main.tsx
│     └─ package.json
├─ tests/
└─ .github/
   └─ workflows/
      └─ deploy.yml
```

## **5. 사용자 요청 처리 흐름**

### **5.1 최초 계획 생성**

1. 사용자가 목적과 상황을 자연어로 입력한다.
2. 클라이언트가 서버에 세션 생성 요청을 보낸다.
3. 서버가 입력을 검증한다.
4. Context Agent가 목적과 제약조건을 추출한다.
5. Discovery Agent가 필요한 업무와 준비물을 제안한다.
6. 제안 결과를 클라이언트에 스트리밍한다.
7. 사용자가 제안 항목을 추가·제외·수정한다.
8. 사용자의 승인 결과를 서버에 전송한다.
9. Task Agent가 승인된 업무를 분류한다.
10. Plan Agent가 실행 순서와 타임라인을 생성한다.
11. Review Agent가 계획의 오류와 경고를 검토한다.
12. 최종 계획을 클라이언트에 전달한다.

### **5.2 업무 상태 변경**

1. 사용자가 업무 카드의 상태 변경 버튼을 누른다.
2. 클라이언트가 서버에 상태 변경 요청을 보낸다.
3. 서버가 허용된 상태 전이인지 검증한다.
4. 세션의 계획 상태를 업데이트한다.
5. 변경된 진행률과 다음 업무를 반환한다.

### **5.3 재계획**

1. 사용자가 현재 상황을 자연어로 입력한다.
2. 서버가 기존 계획과 업무 상태를 조회한다.
3. Recovery Agent가 변경된 조건을 분석한다.
4. 완료된 업무를 재계획 대상에서 제외한다.
5. 남은 업무를 필수·선택·보류로 재분류한다.
6. 변경된 타임라인과 플랜 B를 생성한다.
7. Review Agent가 계획 충돌과 마감 시간을 검토한다.
8. 변경 전·변경 후 결과를 클라이언트에 전달한다.
9. 사용자가 변경된 계획을 승인하거나 수정한다.

## **6. 에이전트 설계**

### **6.1 공통 원칙**

모든 에이전트는 다음 원칙을 따른다.

- 사용자 입력에 없는 사실을 확정하지 않는다.
- 확인하지 않은 운영시간이나 정책을 추측하지 않는다.
- AI가 생성한 내용은 제안 또는 추정으로 표시한다.
- 사용자 승인 전에는 제안 업무를 확정하지 않는다.
- 가능한 경우 구조화된 JSON 형식으로 결과를 반환한다.
- 결과 검증에 실패하면 오류를 반환한다.
- 정보가 부족한 경우 추가 질문을 반환한다.
- 외부 예약, 결제, 신청을 자동으로 실행하지 않는다.

### **6.2 Context Agent**

#### **책임**

- 사용자의 목적 추출
- 반드시 달성해야 하는 목표 추출
- 출발 가능 시간과 귀가 마감 시간 추출
- 장소와 업무 추출
- 걱정 사항과 시간 제약 추출
- 부족한 정보 식별

#### **입력 예시**

```json
{
  "userMessage": "내일 외출해서 부모님 병원 관련 일을 처리해야 해. 오후 3시 전에는 돌아와야 해.",
  "currentTime": "2026-11-05T13:00:00+09:00"
}
```

#### **출력 예시**

```json
{
  "purpose": "부모님 병원 관련 업무 처리",
  "mandatoryGoals": [
    "병원 관련 업무 완료"
  ],
  "departureTime": null,
  "returnDeadline": "15:00",
  "knownPlaces": [],
  "knownTasks": [],
  "constraints": [],
  "concerns": [
    "필요한 서류를 빠뜨릴 가능성"
  ],
  "missingInformation": [
    "병원 업무의 구체적인 내용"
  ],
  "userProvidedFacts": [
    "오후 3시 전 귀가"
  ]
}
```

### **6.3 Discovery Agent**

#### **책임**

- 목적 달성에 필요한 업무 후보 제안
- 준비물과 사전 확인 항목 제안
- 사용자가 놓쳤을 가능성이 있는 행동 발견
- 추가 질문 생성
- 제안 항목을 사용자 승인 전까지 확정하지 않음

#### **출력 예시**

```json
{
  "suggestedTasks": [
    {
      "id": "task-1",
      "title": "병원 방문 준비",
      "reason": "병원 업무를 처리하기 위한 사전 준비",
      "category": "preparation",
      "isAiSuggested": true,
      "requiresUserConfirmation": true
    }
  ],
  "suggestedItems": [
    {
      "id": "item-1",
      "title": "신분증 확인",
      "reason": "사용자가 서류 누락을 걱정하고 있음",
      "isAiSuggested": true,
      "requiresUserConfirmation": true
    }
  ],
  "clarifyingQuestions": [
    "병원에서 직접 진료를 받나요, 서류를 발급받나요?"
  ]
}
```

### **6.4 Task Agent**

승인된 업무를 다음 기준으로 분류한다.

- 필수 업무
- 선택 업무
- 다음 일정으로 미룰 수 있는 업무
- 사전 준비 업무
- 집에서 처리 가능한 업무

처리 원칙:

- 사용자가 반드시 해야 한다고 지정한 업무를 우선한다.
- 시간 제약을 초과하면 선택 업무를 보류 후보로 분류한다.
- 사용자의 필수 업무를 AI가 임의로 삭제하지 않는다.
- 분류 결과에는 판단 근거를 포함한다.

### **6.5 Plan Agent**

다음 결과를 생성한다.

- 업무 실행 순서
- 업무별 예상 시간
- 이동 시간 반영
- 귀가 마감 시간 검토
- 시간순 타임라인
- 필수·선택 업무 구분
- 플랜 B

계획 생성 원칙:

1. 사용자가 지정한 필수 업무를 우선한다.
2. 귀가 마감 시간을 초과하는 계획은 경고와 함께 대안을 제공한다.
3. 시간이 부족하면 선택 업무를 다음 일정으로 이동한다.
4. 입력되지 않은 시간은 추정값으로 표시한다.
5. 실제 운영시간이나 이동 경로를 확인하지 못한 경우 확정하지 않는다.

### **6.6 Review Agent**

다음 항목을 검토한다.

- 일정 충돌
- 귀가 마감 시간 초과
- 필수 업무 누락
- 과도한 업무 포함
- 추정 정보를 사실처럼 표현하는 문장
- 사용자 승인 없이 추가된 업무

```json
{
  "isValid": true,
  "warnings": [
    "이동 시간은 사용자가 입력하지 않아 추정값입니다."
  ],
  "requiredUserConfirmation": false,
  "suggestedChanges": []
}
```

### **6.7 Recovery Agent**

다음 상황에 대응한다.

- 일정 지연
- 업무 취소
- 시간 부족
- 완료된 업무 보존
- 남은 업무 재분류
- 변경된 타임라인 생성
- 다음 일정으로 이동할 업무 제안
- 변경 전·변경 후 차이 설명

### **6.8 내부 결정적 도구**

LLM의 추정에 의존하지 않도록 시간 계산과 검증은 내부 도구로 수행한다.

| **도구** | **역할** |
| --- | --- |
| `calculate_timeline` | 시작 시간·소요 시간·이동 시간으로 일정 계산 |
| `check_deadline` | 귀가 마감 초과 여부 검증 |
| `detect_conflict` | 시간 겹침과 순서 충돌 검증 |

내부 도구는 외부 네트워크를 호출하지 않으며, 모든 입력을 서버에서 검증한다.

## **7. GitHub Copilot SDK 통합**

GitHub Copilot SDK는 모든 에이전트의 모델 실행 계층으로 사용한다.

- 에이전트별 지침과 세션 관리
- 내부 도구 등록과 호출
- 구조화된 JSON 결과 생성
- 스트리밍 응답 전달
- 등록된 내부 도구만 호출하도록 권한 제한

SDK 의존성을 격리하기 위해 어댑터 계층을 사용한다.

```csharp
public interface ICopilotModelClient
{
    Task<T> CompleteAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken);

    IAsyncEnumerable<ModelStreamEvent> StreamAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken);
}
```

실제 SDK 호출은 `CopilotSdkClient`에서 담당한다.

### **7.1 PoC 확인 항목**

구현 전에 다음 항목을 확인한다.

- Azure Container Apps에서 Copilot SDK 런타임과 인증이 동작하는지
- 구조화 출력(JSON)을 지원하는지
- 스트리밍 응답을 지원하는지
- 지원하지 않는 기능에 대한 대체 처리가 필요한지

구조화 출력이 지원되지 않는 경우 서버에서 JSON 스키마 검증과 제한된 재시도를 수행한다.

### **7.2 구조화 출력 검증**

서버는 다음 항목을 검증한다.

- 필수 필드 존재 여부
- 시간 형식
- 업무 상태 enum
- 중복 업무 ID
- 빈 업무 제목
- 잘못된 우선순위 값
- 유효하지 않은 계획 구조

JSON 파싱에 실패하면 빈 계획을 반환하지 않고 오류를 반환한다.

### **7.3 스트리밍 이벤트**

```json
{
  "type": "agent_started",
  "agent": "ContextAgent",
  "message": "입력에서 외출 조건을 분석하고 있습니다."
}
```

```json
{
  "type": "agent_completed",
  "agent": "DiscoveryAgent",
  "data": {}
}
```

```json
{
  "type": "plan_partial",
  "data": {}
}
```

```json
{
  "type": "completed",
  "data": {}
}
```

```json
{
  "type": "error",
  "code": "AI_RESPONSE_INVALID",
  "message": "계획을 생성하지 못했습니다. 다시 시도해 주세요."
}
```

## **8. Microsoft Agent Framework 통합**

### **8.1 오케스트레이션**

최초 계획 생성은 다음 워크플로로 구성한다.

```
Context → Discovery → 사용자 승인 → Task
                                      ├→ Plan ──────────┐
                                      └→ Recovery ──────┴→ Review
```

- Plan 생성과 플랜 B 생성은 병렬 실행할 수 있다.
- 에이전트 간에는 `PlanningContext`의 구조화 데이터만 전달한다.
- 시간 계산과 검증은 내부 결정적 도구를 사용한다.
- Review 실패 시 Plan을 최대 2회까지 재생성한다.
- 재생성 후에도 실패하면 오류를 반환한다.

재계획은 별도의 경로로 실행한다.

```
현재 계획 + 업무 상태 + 사용자 상황 변경
              ↓
       Recovery Agent
              ↓
         Review Agent
              ↓
        변경된 계획
```

완료된 업무는 고정하고 남은 업무만 변경한다.

### **8.2 공통 컨텍스트**

```csharp
public sealed class PlanningContext
{
    public string SessionId { get; init; } = string.Empty;

    public UserIntent? Intent { get; set; }

    public DiscoveryResult? Discovery { get; set; }

    public ApprovalState? Approval { get; set; }

    public List<PlannedTask> Tasks { get; set; } = [];

    public ExecutionPlan? Plan { get; set; }

    public ReviewResult? Review { get; set; }

    public List<ExecutionEvent> Events { get; set; } = [];
}
```

### **8.3 실패 처리**

- 에이전트 실패를 성공 응답으로 변환하지 않는다.
- 재시도 가능한 모델 오류만 제한된 횟수로 재시도한다.
- JSON 스키마 오류는 구조화된 오류로 반환한다.
- 사용자 입력 부족은 추가 질문 상태로 반환한다.
- Review Agent 실패 시 계획을 확정하지 않는다.
- 취소된 요청은 모든 하위 호출에 `CancellationToken`을 전달한다.

## **9. 데이터 모델**

### **9.1 세션**

```json
{
  "sessionId": "session-123",
  "createdAt": "2026-11-05T13:00:00Z",
  "status": "planning",
  "expiresAt": "2026-11-05T15:00:00Z"
}
```

MVP에서는 로그인 없이 세션 ID를 발급한다. 세션은 서버 메모리 또는 브라우저 상태로 관리하며, 일정 시간이 지나면 만료된다.

### **9.2 사용자 의도**

```json
{
  "purpose": "부모님 병원 관련 업무 처리",
  "mandatoryGoals": [
    "병원 업무 완료"
  ],
  "returnDeadline": "15:00",
  "constraints": [
    "필요한 서류를 빠뜨리지 않아야 함"
  ],
  "knownPlaces": [],
  "knownTasks": []
}
```

### **9.3 업무**

```json
{
  "id": "task-1",
  "title": "병원 방문 준비",
  "description": "병원 방문에 필요한 서류와 준비물을 확인한다.",
  "location": "병원",
  "category": "preparation",
  "priority": "required",
  "estimatedMinutes": 20,
  "status": "pending",
  "isAiSuggested": true,
  "requiresUserConfirmation": true
}
```

### **9.4 외출 계획**

```json
{
  "planId": "plan-123",
  "title": "부모님 병원 업무 외출",
  "returnDeadline": "15:00",
  "items": [
    {
      "taskId": "task-1",
      "order": 1,
      "startTime": "09:00",
      "estimatedMinutes": 20,
      "status": "pending"
    }
  ],
  "requiredItems": [],
  "optionalItems": [],
  "fallbackPlan": {
    "trigger": "예상보다 한 시간 지연",
    "actions": [
      "선택 업무를 다음 일정으로 이동"
    ]
  },
  "warnings": [
    "이동 시간은 사용자가 입력하지 않아 추정값입니다."
  ],
  "generatedBy": "ai"
}
```

### **9.5 업무 상태**

허용 상태:

```
pending
in_progress
completed
postponed
cancelled
```

허용 상태 전이:

```
pending → in_progress
pending → cancelled
in_progress → completed
in_progress → postponed
in_progress → cancelled
postponed → pending
```

서버는 허용되지 않은 상태 전이를 거부한다.

## **10. API 설계**

### **10.1 세션 생성**

```
POST /api/sessions
```

응답:

```json
{
  "sessionId": "session-123",
  "expiresAt": "2026-11-05T15:00:00Z"
}
```

### **10.2 업무 및 준비물 제안**

```
POST /api/sessions/{sessionId}/discover
Content-Type: application/json
```

요청:

```json
{
  "message": "내일 외출해서 부모님 병원 관련 일을 처리해야 해. 오후 3시 전에는 돌아와야 해."
}
```

응답:

```
Content-Type: text/event-stream
```

응답 이벤트에는 다음 정보가 포함될 수 있다.

- Context Agent 진행 상태
- 추출된 사용자 의도
- Discovery Agent 진행 상태
- 제안 업무
- 제안 준비물
- 추가 질문
- 오류

### **10.3 업무 제안 승인**

```
POST /api/sessions/{sessionId}/approval
Content-Type: application/json
```

요청:

```json
{
  "approvedTaskIds": [
    "task-1",
    "task-2"
  ],
  "rejectedTaskIds": [
    "task-3"
  ],
  "editedTasks": [],
  "addedTasks": []
}
```

### **10.4 외출 계획 생성**

```
POST /api/sessions/{sessionId}/plan
Content-Type: application/json
```

응답:

```
Content-Type: text/event-stream
```

응답에는 다음 정보가 포함된다.

- 에이전트 진행 상태
- 업무 분류 결과
- 타임라인
- 준비물
- 우선순위
- 플랜 B
- 검토 경고
- 최종 계획

### **10.5 업무 상태 변경**

```
PATCH /api/sessions/{sessionId}/tasks/{taskId}
Content-Type: application/json
```

요청:

```json
{
  "status": "completed"
}
```

응답:

```json
{
  "taskId": "task-1",
  "status": "completed",
  "completedTaskCount": 1,
  "remainingTaskCount": 2,
  "progress": 0.33
}
```

### **10.6 재계획**

```
POST /api/sessions/{sessionId}/replan
Content-Type: application/json
```

요청:

```json
{
  "message": "병원에서 한 시간 늦어졌어."
}
```

응답:

```json
{
  "previousPlan": {},
  "updatedPlan": {},
  "movedTasks": [],
  "warnings": []
}
```

### **10.7 헬스 체크**

```
GET /health
```

응답:

```json
{
  "status": "Healthy"
}
```

헬스 체크 요청에서는 AI 모델을 호출하지 않는다.

### **10.8 오류 응답**

```json
{
  "error": {
    "code": "INVALID_REQUEST",
    "message": "외출 목적을 입력해 주세요.",
    "retryable": false,
    "correlationId": "corr-123"
  }
}
```

## **11. 프론트엔드 설계**

### **11.1 화면 구성**

```
HomePage
    ↓
IntentInputPage
    ↓
DiscoveryPage
    ↓
ApprovalPage
    ↓
PlanningPage
    ↓
ExecutionPlanPage
    ↓
ReplanningPanel
```

### **11.2 화면별 역할**

- **홈 화면**: 서비스 가치, 예시 입력, 새 계획 시작
- **목적 입력 화면**: 자연어 입력, 귀가 마감, 제약조건, 빠른 선택
- **제안 확인 화면**: 목표, 업무, 준비물, 추가 질문, 승인 버튼
- **계획 생성 화면**: 현재 에이전트, 스트리밍 메시지, 취소·재시도
- **실행 계획 화면**: 미션 요약, 타임라인, 체크리스트, 진행률
- **재계획 화면**: 상황 입력, 변경 전·후 비교, 플랜 B, 승인·수정

### **11.3 클라이언트 상태**

```tsx
type AppState =
  | "idle"
  | "discovering"
  | "awaitingApproval"
  | "planning"
  | "executing"
  | "replanning"
  | "error";
```

클라이언트는 다음 상태를 관리한다.

- 현재 세션 ID
- 사용자 입력
- 추출된 사용자 의도
- AI 제안 업무와 준비물
- 사용자 승인 결과
- 현재 계획
- 업무별 실행 상태
- 스트리밍 진행 상태
- 오류 및 재시도 상태

### **11.4 스트리밍 처리**

클라이언트는 SSE 이벤트를 수신하여 다음 정보를 갱신한다.

- 현재 실행 중인 에이전트
- 진행 메시지
- 부분 결과
- 최종 결과
- 오류 상태

스트리밍 연결이 끊기면 무한 재시도하지 않고 사용자에게 재시도 버튼을 제공한다.

## **12. 프롬프트 설계**

### **12.1 공통 시스템 지침**

```
- 사용자가 제공하지 않은 사실을 확정하지 않는다.
- 확인하지 않은 운영시간, 정책, 지원 자격을 추측하지 않는다.
- 추정 또는 제안은 제안으로 표시한다.
- 사용자 승인 전에는 업무를 확정하지 않는다.
- 지정된 JSON 스키마를 따른다.
- 정보가 부족하면 추가 질문을 반환한다.
- 외부 예약, 결제, 신청을 실행하지 않는다.
- 계획 결과에는 필요한 경고를 포함한다.
```

### **12.2 프롬프트 파일 관리**

```
Prompts/
├─ context-agent.txt
├─ discovery-agent.txt
├─ task-agent.txt
├─ plan-agent.txt
├─ review-agent.txt
└─ recovery-agent.txt
```

### **12.3 사용자 입력 처리**

사용자 입력은 신뢰할 수 없는 데이터로 취급한다.

- 시스템 지침과 사용자 입력을 별도로 전달한다.
- 사용자 입력의 명령문이 시스템 지침을 덮어쓰지 못하게 한다.
- 사용자 입력을 HTML로 직접 렌더링하지 않는다.
- 외부 도구 호출은 등록된 내부 도구로 제한한다.

## **13. 보안 및 책임 있는 AI**

### **13.1 비밀 정보 관리**

- API 키를 소스 코드에 저장하지 않는다.
- `.env` 파일을 Git에 커밋하지 않는다.
- Azure 환경 변수 또는 Secret 관리 기능을 사용한다.
- 공개 저장소에 토큰, 비밀번호, 연결 문자열을 포함하지 않는다.
- 로그에 API 키와 민감한 사용자 정보를 기록하지 않는다.

### **13.2 입력 보안**

- 요청 본문 크기를 제한한다.
- 지나치게 긴 입력은 거부하거나 별도 처리한다.
- HTML을 그대로 렌더링하지 않는다.
- 세션 ID와 요청 형식을 서버에서 검증한다.
- CORS 허용 범위를 필요한 도메인으로 제한한다.
- API 요청 빈도 제한을 적용할 수 있도록 구성한다.

### **13.3 프롬프트 인젝션 대응**

- 사용자 입력과 시스템 지침을 분리한다.
- 사용자 입력이 에이전트의 역할과 보안 규칙을 변경하지 못하게 한다.
- 에이전트는 등록된 내부 도구만 호출할 수 있다.
- 외부 네트워크, 셸, 파일 접근 도구는 허용하지 않는다.
- 사용자가 입력하지 않은 기관 정보나 정책을 확인했다고 주장하지 않는다.
- 예약·결제·신청과 같은 외부 작업을 자동 실행하지 않는다.

### **13.4 사용자 통제**

- AI가 제안한 업무는 사용자 승인 전 확정하지 않는다.
- 사용자는 업무를 추가·제외·수정할 수 있다.
- 사용자는 최종 계획을 직접 수정할 수 있다.
- 재계획 결과는 변경 전·변경 후 차이를 보여준다.
- AI 호출 실패 시 기존 계획을 임의로 덮어쓰지 않는다.
- AI 생성 결과는 `AI 제안`으로 표시한다.

## **14. 상태 저장 및 개인정보**

### **14.1 MVP 상태 저장**

MVP에서는 로그인과 영구 저장을 제공하지 않는다.

- 세션 ID를 발급한다.
- 세션 데이터는 서버 메모리 또는 브라우저 상태로 관리한다.
- 세션에는 만료 시간을 설정한다.
- 만료된 세션은 `SESSION_EXPIRED` 오류를 반환한다.
- 영구 데이터베이스는 사용하지 않는다.

### **14.2 데이터 처리 원칙**

- 민감한 개인정보 입력을 권장하지 않는다.
- 서버 로그에 사용자 입력 원문을 저장하지 않는다.
- API 요청과 응답에 필요한 최소한의 데이터만 처리한다.
- 브라우저 종료 또는 세션 만료 후 데이터가 유지되지 않을 수 있음을 안내한다.
- 영구 저장을 추가할 경우 삭제 정책과 개인정보 처리 방식을 별도로 검토한다.

## **15. 오류 처리 및 관찰성**

### **15.1 로그 항목**

- 요청 경로
- HTTP 상태 코드
- 상관관계 ID
- 비식별화된 세션 ID
- 에이전트 이름
- 에이전트 실행 시간
- 성공·실패 여부
- 오류 코드
- 재시도 횟수

사용자 입력 원문과 AI 응답 전체는 기본 로그에 기록하지 않는다.

### **15.2 오류 코드**

```
INVALID_REQUEST
SESSION_NOT_FOUND
SESSION_EXPIRED
AI_REQUEST_FAILED
AI_RESPONSE_INVALID
AGENT_TIMEOUT
PLAN_VALIDATION_FAILED
STREAM_INTERRUPTED
RATE_LIMITED
INTERNAL_ERROR
```

### **15.3 오류 처리 원칙**

- 오류를 빈 계획으로 대체하지 않는다.
- AI 호출 실패를 성공 응답으로 숨기지 않는다.
- 사용자에게 재시도 가능 여부를 표시한다.
- 계획 생성 중 실패하면 기존 계획을 보존한다.
- 오류에 상관관계 ID를 연결한다.
- 내부 예외의 상세 스택 트레이스는 공개하지 않는다.

### **15.4 Application Insights**

다음 정보를 관찰한다.

- API 요청 수
- HTTP 오류 수
- 요청 응답 시간
- AI 호출 시간
- 에이전트별 실행 시간
- 스트리밍 연결 오류
- 예외 발생 내역

사용자 입력 원문과 비밀 정보는 텔레메트리에 포함하지 않는다.

## **16. Azure 및 Aspire 배포**

### **16.1 Aspire 구성**

```
Ilhaengro.AppHost
├─ ilhaengro-api
└─ ilhaengro-web
```

로컬 환경에서 API, 웹앱, 서비스 간 연결, 환경 변수, 요청 로그, 오류 상태를 확인할 수 있어야 한다.

### **16.2 Azure 배포 구조**

```
Azure Container Apps
├─ ilhaengro-web
└─ ilhaengro-api
```

웹앱은 Azure 기본 도메인으로 공개한다. API는 웹앱에서 필요한 요청만 허용하도록 CORS를 제한한다.

### **16.3 환경 변수**

```
COPILOT_ENDPOINT
COPILOT_MODEL
COPILOT_API_KEY
APPLICATIONINSIGHTS_CONNECTION_STRING
ASPNETCORE_ENVIRONMENT
ALLOWED_ORIGINS
```

실제 환경 변수 값은 소스 코드와 공개 문서에 저장하지 않는다. 비밀 값은 Azure Key Vault 또는 Container Apps Secret으로 주입한다.

### **16.4 반복 가능한 배포**

- 인프라는 Bicep으로 정의한다.
- `azd up`으로 환경을 재현할 수 있도록 구성한다.
- GitHub Actions는 `azd`를 사용해 배포한다.
- 심사 제출 커밋과 배포된 코드의 버전을 일치시킨다.

### **16.5 비밀 정보와 안정성**

- 비밀 정보는 Azure Secret 또는 Key Vault에 저장한다.
- API 컨테이너에 `/health` 프로브를 설정한다.
- 메모리 세션을 사용하는 동안 API 복제본은 1개로 유지한다.
- Application Insights에 응답 시간과 오류율을 기록한다.

### **16.6 배포 전 확인**

- 웹앱 공개 접속
- API 헬스 체크
- AI 호출
- SSE 스트리밍
- CORS
- 오류 화면
- 세션 만료 처리
- Application Insights 로그
- Azure 기본 도메인
- 로그인 없이 핵심 흐름 사용 가능 여부

## **17. 테스트 계획**

### **17.1 기능 테스트**

| **테스트 항목** | **기대 결과** |
| --- | --- |
| 목적과 상황 입력 | Context Agent가 입력을 분석한다. |
| 업무 후보 제안 | Discovery Agent 결과가 표시된다. |
| 제안 항목 제외 | 제외한 항목이 최종 계획에 포함되지 않는다. |
| 제안 항목 수정 | 수정된 내용이 계획에 반영된다. |
| 업무 직접 추가 | 사용자가 추가한 업무가 계획에 포함된다. |
| 필수 업무 지정 | 필수 업무가 우선순위에 반영된다. |
| 계획 생성 | 타임라인과 플랜 B가 표시된다. |
| 업무 완료 | 진행률과 남은 업무가 갱신된다. |
| 업무 취소 | 해당 업무가 재계획에서 제외된다. |
| 일정 지연 입력 | Recovery Agent가 재계획한다. |
| 계획 수정 | 사용자 수정 결과가 유지된다. |
| 효과 측정 | 5개 시나리오를 수동 계획과 비교한다. |

### **17.2 책임 있는 AI 테스트**

- 입력하지 않은 운영시간을 사실처럼 확정하지 않는지 확인한다.
- 사용자 승인 전 제안 업무가 확정되지 않는지 확인한다.
- 정보가 부족할 때 추가 질문 또는 확인 필요 상태가 반환되는지 확인한다.
- 프롬프트 인젝션 입력이 시스템 지침을 변경하지 못하는지 확인한다.
- 외부 예약·결제·신청을 자동으로 실행하지 않는지 확인한다.
- AI 생성 결과가 명확히 표시되는지 확인한다.
- 추정 시간과 사용자가 입력한 시간을 구분하는지 확인한다.
- 키보드 조작과 스크린리더에서 핵심 흐름이 동작하는지 확인한다.

### **17.3 오류 테스트**

- AI API 오류
- 잘못된 JSON 응답
- 스트리밍 연결 끊김
- 세션 만료
- 빈 입력
- 지나치게 긴 입력
- 귀가 마감 시간을 초과하는 계획
- 잘못된 업무 상태 전이
- 중복 요청
- 네트워크 오류
- API 타임아웃

### **17.4 Azure 스모크 테스트**

```
1. 로그인 없이 웹앱에 접속한다.
2. 목적과 상황을 입력한다.
3. AI 제안을 확인한다.
4. 업무 일부를 제외한다.
5. 외출 계획을 생성한다.
6. 업무 하나를 완료한다.
7. 일정 지연을 입력한다.
8. 재계획 결과를 확인한다.
9. 오류 및 재시도 동작을 확인한다.
10. 헬스 체크를 확인한다.
```

## **18. 성능 및 운영 기준**

- 홈 화면 최초 로딩: 3초 이내
- AI 요청 시작 상태 표시: 1초 이내
- AI 응답은 가능한 경우 스트리밍으로 표시
- 단일 AI 요청 타임아웃: 60초
- 동일 요청 무한 재시도 금지
- 사용자 입력 최대 길이 제한
- 동시 세션 수 제한
- 생성 중 취소 요청 지원
- 계획 생성 실패 시 기존 상태 보존

AI 응답 시간은 모델과 네트워크에 따라 달라질 수 있으므로, 현재 처리 단계와 진행 메시지를 표시한다.

## **19. 기술적 제한사항**

- 외부 기관 운영시간을 자동으로 확인하지 않는다.
- 실제 이동 경로와 최단 시간을 계산하지 않는다.
- AI가 제안하는 준비물과 계획은 참고용이다.
- 이동 시간은 사용자가 입력하거나 추정값으로 표시한다.
- 로그인하지 않은 세션은 영구 보존되지 않는다.
- 세션 만료 후 이전 계획을 복구하지 못할 수 있다.
- AI 모델 또는 네트워크 오류가 발생할 수 있다.
- 사용자는 최종 계획을 직접 확인하고 수정해야 한다.
- 실제 예약·결제·민원 신청은 지원하지 않는다.
- 메모리 세션을 사용하는 동안 API 복제본은 1개로 고정한다.
- API가 재시작되면 메모리에 저장된 세션이 유실될 수 있다.