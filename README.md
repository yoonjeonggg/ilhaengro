# 일행로

목적만 말하면 준비물, 업무 순서, 타임라인과 플랜 B를 제안하는 AI 외출 플래너입니다.

## 로컬 실행

PowerShell에서 .NET과 Aspire CLI가 PATH에 포함되어 있어야 합니다.

```powershell
cd C:\dev\ilhaengro
dotnet build Ilhaengro.slnx
aspire run --project src\Ilhaengro.AppHost\Ilhaengro.AppHost.csproj
```

프론트엔드만 확인하려면 다음을 실행합니다.

```powershell
npm install
npm run dev
```

## AI 실행 설정

API는 확인된 `GitHub.Copilot.SDK` 1.0.28을 사용합니다. Copilot CLI 인증이 완료된 환경에서 실행해야 실제 Context/Discovery/Plan/Review 에이전트 호출이 동작합니다.

```powershell
$env:COPILOT_MODEL = "gpt-5"
copilot
```

`COPILOT_MODEL`은 선택 설정이며, 비밀 값과 토큰은 저장소에 기록하지 않습니다. Copilot SDK가 시작되지 않거나 JSON 응답이 유효하지 않으면 API는 빈 계획으로 대체하지 않고 오류를 반환합니다.

## 주요 구성

- `src\Ilhaengro.Api`: ASP.NET Core API, 메모리 세션, Copilot SDK 기반 에이전트 오케스트레이션
- `src\Ilhaengro.AppHost`: .NET Aspire로 API와 Vite 웹앱 연결
- `src\App.tsx`: 자연어 입력, 제안 승인, 계획 실행, 재계획 UI
- `src\services\api.ts`: API 계약과 명시적 오류 처리

로그인, 영구 데이터베이스, 지도 API, 캘린더 연동은 구현하지 않습니다.