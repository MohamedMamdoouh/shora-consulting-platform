import { Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminOpsAlertDto, AdminOpsRunbookDto } from '@contracts/ops';
import { forkJoin, firstValueFrom } from 'rxjs';
import { readApiError } from '../../core/api/api-error.util';
import { AdminOpsService } from '../../core/admin/admin-ops.service';
import {
  compareAlertsBySeverity,
  countAlertsBySeverity,
  formatAlertKind,
  formatAlertSeverity,
  formatContextEntries,
  getAlertActionRoute,
  severityCssModifier,
} from './admin-ops-labels.util';

type PageState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | {
      status: 'ready';
      alerts: AdminOpsAlertDto[];
      runbooksById: Map<string, AdminOpsRunbookDto>;
      counts: { critical: number; warning: number };
    };

@Component({
  selector: 'app-admin-ops-page',
  imports: [RouterLink],
  templateUrl: './admin-ops-page.component.html',
  styleUrl: './admin-ops-page.component.scss',
})
export class AdminOpsPageComponent implements OnInit {
  private readonly adminOpsService = inject(AdminOpsService);

  pageState: PageState = { status: 'loading' };

  readonly formatAlertKind = formatAlertKind;
  readonly formatAlertSeverity = formatAlertSeverity;
  readonly formatContextEntries = formatContextEntries;
  readonly getAlertActionRoute = getAlertActionRoute;
  readonly severityCssModifier = severityCssModifier;

  ngOnInit(): void {
    void this.loadOpsData();
  }

  async loadOpsData(): Promise<void> {
    this.pageState = { status: 'loading' };

    try {
      const { alerts, runbooks } = await firstValueFrom(
        forkJoin({
          alerts: this.adminOpsService.getAlerts(),
          runbooks: this.adminOpsService.getRunbooks(),
        }),
      );

      const runbooksById = new Map(runbooks.runbooks.map((runbook) => [runbook.id, runbook]));
      const sortedAlerts = [...alerts.alerts].sort(compareAlertsBySeverity);

      this.pageState = {
        status: 'ready',
        alerts: sortedAlerts,
        runbooksById,
        counts: countAlertsBySeverity(sortedAlerts),
      };
    } catch (error) {
      this.pageState = {
        status: 'error',
        message: readApiError(error, 'تعذّر تحميل تنبيهات التشغيل. حاول مرة أخرى.'),
      };
    }
  }

  getRunbook(runbookId: string): AdminOpsRunbookDto | undefined {
    if (this.pageState.status !== 'ready') {
      return undefined;
    }

    return this.pageState.runbooksById.get(runbookId);
  }
}
