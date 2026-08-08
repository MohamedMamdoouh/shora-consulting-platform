export interface AdminOpsAlertDto {
  kind: string;
  severity: string;
  message: string;
  runbookId: string;
  context: Record<string, string>;
}

export interface AdminOpsAlertsResponse {
  alerts: AdminOpsAlertDto[];
}

export interface AdminOpsRunbookDto {
  id: string;
  owner: string;
  responseSla: string;
  trigger: string;
  steps: string[];
}

export interface AdminOpsRunbooksResponse {
  runbooks: AdminOpsRunbookDto[];
}
