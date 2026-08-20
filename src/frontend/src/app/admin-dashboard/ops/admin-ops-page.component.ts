import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminOpsAlertDto, AdminOpsRunbookDto } from '@contracts/ops';
import { forkJoin, firstValueFrom } from 'rxjs';
import { readApiError } from '../../core/api/api-error.util';
import { AdminOpsService } from '../../core/admin/admin-ops.service';
import {
  compareAlertsBySeverity,
  countAlertsBySeverity,
  formatAlertKind,
  formatAlertMessage,
  formatAlertSeverity,
  formatContextEntries,
  getAlertActionRoute,
  localizeRunbook,
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

  readonly pageState = signal<PageState>({ status: 'loading' });

  readonly formatAlertKind = formatAlertKind;
  readonly formatAlertMessage = formatAlertMessage;
  readonly formatAlertSeverity = formatAlertSeverity;
  readonly formatContextEntries = formatContextEntries;
  readonly getAlertActionRoute = getAlertActionRoute;
  readonly localizeRunbook = localizeRunbook;
  readonly severityCssModifier = severityCssModifier;

  ngOnInit(): void {
    void this.loadOpsData();
  }

  async loadOpsData(): Promise<void> {
    this.pageState.set({ status: 'loading' });

    try {
      const { alerts, runbooks } = await firstValueFrom(
        forkJoin({
          alerts: this.adminOpsService.getAlerts(),
          runbooks: this.adminOpsService.getRunbooks(),
        }),
      );

      const runbooksById = new Map(runbooks.runbooks.map((runbook) => [runbook.id, runbook]));
      const sortedAlerts = [...alerts.alerts].sort(compareAlertsBySeverity);

      this.pageState.set({
        status: 'ready',
        alerts: sortedAlerts,
        runbooksById,
        counts: countAlertsBySeverity(sortedAlerts),
      });
    } catch (error) {
      this.pageState.set({
        status: 'error',
        message: readApiError(error, 'تعذر تحميل تنبيهات النظام. حاول مرة أخرى.'),
      });
    }
  }

  getRunbook(runbookId: string): AdminOpsRunbookDto | undefined {
    const state = this.pageState();
    if (state.status !== 'ready') {
      return undefined;
    }

    return state.runbooksById.get(runbookId);
  }
}
