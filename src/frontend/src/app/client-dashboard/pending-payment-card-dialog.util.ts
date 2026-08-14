import { ResultDialogOptions } from '../core/ui/confirm-dialog.service';

export function buildReceiptUploadedResult(
  message: string,
  onDashboardRefresh: () => void,
): ResultDialogOptions {
  return {
    message,
    redirectTo: ['/dashboard'],
    onComplete: onDashboardRefresh,
  };
}
