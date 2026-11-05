import type { MissionPlan, SuggestedTask, SupplyItem, TaskStatus } from "../types";

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? "";

export class ApiError extends Error {
  constructor(message: string, public readonly code = "API_UNAVAILABLE") {
    super(message);
    this.name = "ApiError";
  }
}

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 65000);
  const signal = options?.signal ?? controller.signal;
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      headers: { "Content-Type": "application/json" },
      ...options,
      signal,
    });
  } finally {
    clearTimeout(timeout);
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null) as { error?: { message?: string; code?: string } } | null;
    throw new ApiError(body?.error?.message ?? "서버와 통신하지 못했습니다. 잠시 후 다시 시도해 주세요.", body?.error?.code);
  }
  return response.json() as Promise<T>;
}

export const createSession = () => request<{ sessionId: string }>("/api/sessions", { method: "POST", body: "{}" });
export const discover = (sessionId: string, message: string) => request<{ tasks: SuggestedTask[]; supplies: SupplyItem[] }>(`/api/sessions/${sessionId}/discover`, { method: "POST", body: JSON.stringify({ message }) });
export const createPlan = (sessionId: string, tasks: SuggestedTask[], supplies: SupplyItem[], returnDeadline: string) => request<MissionPlan>(`/api/sessions/${sessionId}/plan`, { method: "POST", body: JSON.stringify({ tasks, supplies, returnDeadline }) });
export const updateTask = (sessionId: string, taskId: string, status: TaskStatus) => request<{ taskId: string; status: TaskStatus }>(`/api/sessions/${sessionId}/tasks/${taskId}`, { method: "PATCH", body: JSON.stringify({ status }) });
export const replan = (sessionId: string, message: string) => request<{ previousPlan: MissionPlan; updatedPlan: MissionPlan; movedTasks: string[]; warnings: string[] }>(`/api/sessions/${sessionId}/replan`, { method: "POST", body: JSON.stringify({ message }) });
