export interface AdminEarningsQuery {
  from?: string;
  to?: string;
}

export interface AdminEarningsResponse {
  grossRevenue: number;
  refundedAmount: number;
  netRevenue: number;
  approvedCount: number;
  refundedCount: number;
  refundDueCount: number;
}
