import { Routes } from '@angular/router';
import { clientGuard } from '../core/auth/auth.guard';
import { ClientDashboardComponent } from './client-dashboard.component';

export const CLIENT_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    canActivate: [clientGuard],
    component: ClientDashboardComponent,
    data: { title: 'لوحة العميل' },
  },
];
