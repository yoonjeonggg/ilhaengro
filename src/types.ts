export type TaskStatus = "pending" | "completed" | "postponed" | "cancelled";
export type TaskPriority = "required" | "optional";

export interface SuggestedTask {
  id: string;
  title: string;
  reason: string;
  category: string;
  priority: TaskPriority;
  estimatedMinutes: number;
  selected: boolean;
}

export interface SupplyItem {
  id: string;
  title: string;
  reason: string;
  checked: boolean;
}

export interface PlannedTask extends SuggestedTask {
  status: TaskStatus;
  startTime: string;
}

export interface MissionPlan {
  title: string;
  returnDeadline: string;
  tasks: PlannedTask[];
  supplies: SupplyItem[];
  fallback: string;
  warning?: string;
}
