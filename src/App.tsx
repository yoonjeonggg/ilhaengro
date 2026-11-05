import { useMemo, useState } from "react";
import { ApiError, addTask, carryOver, createPlan, createSession, discover, reorderTasks, replan, updateTask } from "./services/api";
import type { MissionPlan, SuggestedTask, SupplyItem, TaskStatus } from "./types";

type View = "home" | "suggestions" | "plan";
const initialTasks: SuggestedTask[] = [
  { id: "hospital", title: "병원 업무 확인", reason: "외출 목적에 포함된 핵심 업무입니다.", category: "방문", priority: "required", estimatedMinutes: 45, selected: true },
  { id: "bank", title: "은행 업무 처리", reason: "입력한 두 번째 방문 목적입니다.", category: "방문", priority: "required", estimatedMinutes: 35, selected: true },
  { id: "documents", title: "필요 서류 사전 확인", reason: "서류를 빠뜨릴 걱정을 줄이기 위한 준비입니다.", category: "준비", priority: "optional", estimatedMinutes: 15, selected: true },
];
const initialSupplies: SupplyItem[] = [
  { id: "id-card", title: "신분증", reason: "기관 방문 시 확인이 필요할 수 있습니다.", checked: false },
  { id: "documents", title: "관련 서류", reason: "정확한 서류 종류는 기관에 확인해 주세요.", checked: false },
];

function App() {
  const [view, setView] = useState<View>("home");
  const [message, setMessage] = useState("");
  const [deadline, setDeadline] = useState("15:00");
  const [tasks, setTasks] = useState(initialTasks);
  const [supplies, setSupplies] = useState(initialSupplies);
  const [plan, setPlan] = useState<MissionPlan | null>(null);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [replanMessage, setReplanMessage] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("AI 제안은 승인하기 전까지 확정되지 않습니다.");
  const selectedTasks = useMemo(() => tasks.filter((task) => task.selected), [tasks]);

  async function startDiscovery() {
    if (message.trim().length < 5) { setError("외출 목적과 상황을 조금 더 자세히 입력해 주세요."); return; }
    setError(""); setLoading(true); setNotice("입력에서 목적과 시간 제약을 분석하고 있습니다.");
    try {
      const session = await createSession(); setSessionId(session.sessionId);
      const result = await discover(session.sessionId, message); setTasks(result.tasks); setSupplies(result.supplies); setView("suggestions");
    } catch (requestError) {
      setSessionId(null); setNotice("AI 제안을 불러오지 못했습니다. 입력 내용은 유지되며 다시 시도할 수 있습니다.");
      setError(requestError instanceof ApiError ? requestError.message : "제안을 불러오지 못했습니다. 다시 시도해 주세요.");
      return;
    } finally { setLoading(false); }
  }

  async function approveAndPlan() {
    if (selectedTasks.length === 0) { setError("계획에 포함할 업무를 하나 이상 선택해 주세요."); return; }
    setError(""); setLoading(true); setNotice("승인한 업무의 순서와 시간을 계산하고 있습니다.");
    try {
      const result = sessionId ? await createPlan(sessionId, selectedTasks, supplies, deadline) : buildLocalPlan(selectedTasks, supplies, deadline);
      setPlan(result); setView("plan");
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : "계획을 생성하지 못했습니다."); }
    finally { setLoading(false); }
  }

  async function changeStatus(taskId: string, status: TaskStatus) {
    const previousPlan = plan;
    setPlan((current) => current ? { ...current, tasks: current.tasks.map((task) => task.id === taskId ? { ...task, status } : task) } : current);
    if (sessionId) {
      try {
        await updateTask(sessionId, taskId, { status });
      } catch (requestError) {
        setPlan(previousPlan);
        setError(requestError instanceof ApiError ? requestError.message : "업무 상태를 저장하지 못했습니다.");
      }

    }
  }

  async function editTask(taskId: string) {
    const task = plan?.tasks.find((item) => item.id === taskId);
    if (!task || !sessionId) return;
    const title = window.prompt("업무 제목", task.title)?.trim();
    if (!title) return;
    const estimatedMinutes = Number(window.prompt("예상 시간(분)", String(task.estimatedMinutes)));
    if (!Number.isInteger(estimatedMinutes) || estimatedMinutes < 1 || estimatedMinutes > 480) {
      setError("예상 시간은 1분에서 480분 사이의 정수로 입력해 주세요.");
      return;
    }
    const priority = window.confirm("이 업무를 필수 업무로 지정할까요?") ? "required" : "optional";
    try {
      await updateTask(sessionId, taskId, { title, priority, estimatedMinutes });
      setPlan((current) => current ? { ...current, tasks: current.tasks.map((item) => item.id === taskId ? { ...item, title, priority, estimatedMinutes } : item) } : current);
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : "업무를 수정하지 못했습니다."); }
  }

  async function addManualTask() {
    if (!plan || !sessionId) return;
    const title = window.prompt("추가할 업무")?.trim();
    if (!title) return;
    const task: SuggestedTask = { id: `manual-${Date.now()}`, title, reason: "사용자가 직접 추가한 업무입니다.", category: "직접 추가", priority: "required", estimatedMinutes: 20, selected: true };
    try { setPlan(await addTask(sessionId, task)); } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : "업무를 추가하지 못했습니다."); }
  }

  async function reorder(taskIds: string[]) {
    if (!sessionId) return;
    try { setPlan(await reorderTasks(sessionId, taskIds)); } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : "업무 순서를 저장하지 못했습니다."); }
  }

  async function handleReplan() {
    if (!replanMessage.trim() || !plan) return;
    setLoading(true); setError("");
    try {
      let nextPlan: MissionPlan;
      if (sessionId) {
        nextPlan = (await replan(sessionId, replanMessage)).updatedPlan;
      } else {
        const completedTasks = plan.tasks.filter((task) => task.status === "completed" || task.status === "cancelled");
        const remainingPlan = buildLocalPlan(plan.tasks.filter((task) => task.status !== "completed" && task.status !== "cancelled"), plan.supplies, plan.returnDeadline, replanMessage);
        nextPlan = { ...remainingPlan, tasks: [...completedTasks, ...remainingPlan.tasks] };
      }
      setPlan(nextPlan); setReplanMessage(""); setNotice("완료된 업무는 유지하고 남은 업무만 다시 계산했습니다.");
    } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : "재계획을 생성하지 못했습니다."); }
    finally { setLoading(false); }
  }

  return <main className="app-shell"><header className="topbar"><button className="brand" onClick={() => setView("home")} aria-label="일행로 홈"><span className="brand-mark">일</span><span>일행로</span></button><span className="header-note">AI 외출 플래너</span></header><section className="content">
    {view === "home" && <Home message={message} setMessage={setMessage} deadline={deadline} setDeadline={setDeadline} onStart={startDiscovery} loading={loading} />}
    {view === "suggestions" && <Suggestions tasks={tasks} supplies={supplies} setTasks={setTasks} setSupplies={setSupplies} onPlan={approveAndPlan} onBack={() => setView("home")} loading={loading} />}
    {view === "plan" && plan && <PlanView plan={plan} onStatus={changeStatus} onEdit={editTask} onAdd={addManualTask} onReorder={reorder} onCarryOver={async (ids) => { if (!sessionId || !window.confirm("선택한 업무를 다음 외출로 이월할까요?")) return; try { const next = await carryOver(sessionId, ids); setSessionId(next.sessionId); setPlan(next.plan); setNotice("업무를 다음 외출 미션으로 이월했습니다."); } catch (requestError) { setError(requestError instanceof ApiError ? requestError.message : "업무를 이월하지 못했습니다."); } }} replanMessage={replanMessage} setReplanMessage={setReplanMessage} onReplan={handleReplan} loading={loading} onBack={() => setView("suggestions")} />}
    {notice && <p className="notice"><span className="spark">✦</span>{notice}</p>}{error && <div className="error" role="alert">{error}</div>}
  </section></main>;
}

function Home({ message, setMessage, deadline, setDeadline, onStart, loading }: { message: string; setMessage: (value: string) => void; deadline: string; setDeadline: (value: string) => void; onStart: () => void; loading: boolean }) {
  return <section className="hero"><div className="eyebrow">오늘의 외출을, 한 번에</div><h1>목적만 말하면<br /><em>일행로</em>가 정리해 드려요</h1><p className="hero-copy">해야 할 일부터 준비물, 시간 순서와 플랜 B까지<br />당신의 외출을 놓치지 않게 함께 계획합니다.</p><div className="input-card"><label htmlFor="purpose">어디에, 무엇을 하러 가나요?</label><textarea id="purpose" value={message} onChange={(event) => setMessage(event.target.value)} placeholder="예: 내일 병원과 은행에 가야 하고, 오후 3시 전에는 돌아와야 해." rows={4} /><div className="input-footer"><label className="deadline">귀가 마감 <input type="time" value={deadline} onChange={(event) => setDeadline(event.target.value)} /></label><button className="primary-button" onClick={onStart} disabled={loading}>{loading ? "분석 중..." : "계획 시작하기"} <span>→</span></button></div></div><div className="examples"><span>이렇게 말해보세요</span><button onClick={() => setMessage("내일 부모님 병원에 모시고 은행 업무도 봐야 해. 오후 3시 전 귀가가 목표야.")}>“병원과 은행, 3시 전 귀가”</button><button onClick={() => setMessage("동사무소에서 서류를 발급받고 시장에 들러 장을 봐야 해.")}>“서류 발급하고 장보기”</button></div></section>;
}

function Suggestions({ tasks, supplies, setTasks, setSupplies, onPlan, onBack, loading }: { tasks: SuggestedTask[]; supplies: SupplyItem[]; setTasks: React.Dispatch<React.SetStateAction<SuggestedTask[]>>; setSupplies: React.Dispatch<React.SetStateAction<SupplyItem[]>>; onPlan: () => void; onBack: () => void; loading: boolean }) {
  return <section className="workspace"><button className="back-button" onClick={onBack}>← 목적 수정</button><div className="section-heading"><div><div className="eyebrow">STEP 1 · AI 제안</div><h2>이런 일들을 준비해 볼까요?</h2></div><span className="ai-pill">✦ AI 제안</span></div><p className="section-copy">입력하신 목적을 바탕으로 필요한 업무와 준비물을 정리했어요.<br />원하지 않는 항목은 제외하거나 내용을 직접 수정할 수 있습니다.</p><div className="suggestion-grid"><div className="panel"><div className="panel-title"><span>처리할 업무</span><span className="count">{tasks.filter((task) => task.selected).length}개 선택</span></div>{tasks.map((task) => <label className={`suggestion-row ${task.selected ? "selected" : ""}`} key={task.id}><input type="checkbox" checked={task.selected} onChange={() => setTasks((current) => current.map((item) => item.id === task.id ? { ...item, selected: !item.selected } : item))} /><span className="checkmark">✓</span><span className="row-body"><strong>{task.title}</strong><small>{task.reason}</small></span><span className={`tag ${task.priority}`}>{task.priority === "required" ? "필수" : "선택"}</span></label>)}</div><div className="panel"><div className="panel-title"><span>준비물 & 확인사항</span><span className="count">{supplies.filter((item) => item.checked).length}/{supplies.length}</span></div>{supplies.map((item) => <label className="suggestion-row supply" key={item.id}><input type="checkbox" checked={item.checked} onChange={() => setSupplies((current) => current.map((value) => value.id === item.id ? { ...value, checked: !value.checked } : value))} /><span className="checkmark">✓</span><span className="row-body"><strong>{item.title}</strong><small>{item.reason}</small></span></label>)}</div></div><div className="action-bar"><span><span className="spark">✦</span> 승인한 항목만 최종 계획에 포함됩니다.</span><button className="primary-button" onClick={onPlan} disabled={loading}>{loading ? "계획 생성 중..." : "이대로 계획 만들기"} <span>→</span></button></div></section>;
}

function PlanView({ plan, onStatus, onEdit, onAdd, onReorder, onCarryOver, replanMessage, setReplanMessage, onReplan, loading, onBack }: { plan: MissionPlan; onStatus: (id: string, status: TaskStatus) => void; onEdit: (id: string) => void; onAdd: () => void; onReorder: (ids: string[]) => void; onCarryOver: (ids: string[]) => void; replanMessage: string; setReplanMessage: (value: string) => void; onReplan: () => void; loading: boolean; onBack: () => void }) {
  const completed = plan.tasks.filter((task) => task.status === "completed").length;
  const carryable = plan.tasks.filter((task) => task.status === "postponed" || task.status === "cancelled").map((task) => task.id);
  return <section className="workspace">
    <button className="back-button" onClick={onBack}>← 제안으로 돌아가기</button>
    <div className="section-heading"><div><div className="eyebrow">STEP 2 · 실행 계획</div><h2>{plan.title}</h2></div><span className="deadline-badge">귀가 목표 {plan.returnDeadline}</span></div>
    <div className="progress-card"><div><span className="muted">오늘의 진행률</span><strong>{Math.round((completed / Math.max(plan.tasks.length, 1)) * 100)}%</strong></div><div className="progress-track"><span style={{ width: `${(completed / Math.max(plan.tasks.length, 1)) * 100}%` }} /></div><small>{completed}개 완료 · {plan.tasks.length - completed}개 남음</small></div>
    <div className="action-bar plan-actions"><span>업무를 직접 추가하거나 순서를 바꿀 수 있어요.</span><button className="primary-button" onClick={onAdd}>+ 업무 추가</button></div>
    <div className="plan-layout"><div className="panel timeline"><div className="panel-title"><span>시간순 타임라인</span><span className="muted">예상 시간</span></div>{plan.tasks.map((task, index) => <div className={`timeline-row ${task.status}`} key={task.id}><div className="time">{task.startTime}</div><div className="timeline-dot" /><div className="timeline-content"><strong>{task.title}</strong><small>{task.estimatedMinutes}분 · {task.priority === "required" ? "필수 업무" : "선택 업무"}</small>    <div className="task-actions"><button onClick={() => onStatus(task.id, task.status === "completed" ? "pending" : "completed")}>{task.status === "completed" ? "완료 취소" : "완료"}</button><button onClick={() => onStatus(task.id, "postponed")}>보류</button><button onClick={() => onStatus(task.id, "cancelled")}>취소</button><button onClick={() => onEdit(task.id)}>수정·필수 지정</button><button onClick={() => index > 0 && onReorder(plan.tasks.map((item) => item.id).map((id, position, ids) => position === index - 1 ? ids[index] : position === index ? ids[index - 1] : id))}>↑</button><button onClick={() => index < plan.tasks.length - 1 && onReorder(plan.tasks.map((item) => item.id).map((id, position, ids) => position === index ? ids[index + 1] : position === index + 1 ? ids[index] : id))}>↓</button></div></div></div>)}</div><aside className="side-column"><div className="panel"><div className="panel-title">준비물 체크리스트</div>{plan.supplies.map((item) => <label className="check-row" key={item.id}><input type="checkbox" checked={item.checked} readOnly /><span>{item.title}</span></label>)}</div><div className="fallback"><div className="panel-title"><span>플랜 B</span><span className="spark">✦</span></div><p>{plan.fallback}</p></div></aside></div>
    {carryable.length > 0 && <div className="action-bar"><span>보류·취소된 업무 {carryable.length}개</span><button className="primary-button" onClick={() => onCarryOver(carryable)}>다음 외출로 이월</button></div>}
    <div className="replan-card"><div><div className="eyebrow">상황이 바뀌었나요?</div><strong>자연어로 알려주면 남은 계획을 다시 짜드려요.</strong></div><div className="replan-input"><input value={replanMessage} onChange={(event) => setReplanMessage(event.target.value)} placeholder="예: 병원에서 한 시간 늦어졌어." /><button onClick={onReplan} disabled={loading || !replanMessage.trim()}>{loading ? "재계획 중" : "재계획"} →</button></div></div>{plan.warning && <p className="warning">⚠ {plan.warning}</p>}
  </section>;
}

function buildLocalPlan(tasks: SuggestedTask[], supplies: SupplyItem[], deadline: string, change = ""): MissionPlan {
  let minute = 9 * 60;
  const plannedTasks = tasks.map((task) => { const startTime = `${String(Math.floor(minute / 60)).padStart(2, "0")}:${String(minute % 60).padStart(2, "0")}`; minute += task.estimatedMinutes + 15; return { ...task, status: "pending" as const, startTime }; });
  return { title: "오늘의 외출 계획", returnDeadline: deadline, tasks: plannedTasks, supplies, fallback: change ? "시간이 지연되어 선택 업무는 다음 일정으로 미루는 것을 권합니다." : "시간이 부족해지면 선택 업무를 다음 일정으로 옮기고 필수 업무를 먼저 처리하세요.", warning: "이동 시간과 기관 운영시간은 확인되지 않은 추정값입니다." };
}

export default App;
