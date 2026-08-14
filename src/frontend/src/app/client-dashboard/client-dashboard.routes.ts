import { Routes } from '@angular/router';
import { clientGuard } from '../core/auth/auth.guard';
import { ClientDashboardComponent } from './client-dashboard.component';
import { HistoryBookingsComponent } from './history-bookings.component';
import { PendingBookingsComponent } from './pending-bookings.component';
import { UpcomingBookingsComponent } from './upcoming-bookings.component';

export const CLIENT_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    canActivate: [clientGuard],
    component: ClientDashboardComponent,
    children: [
      { path: '', redirectTo: 'upcoming', pathMatch: 'full' },
      {
        path: 'upcoming',
        component: UpcomingBookingsComponent,
      },
      {
        path: 'pending',
        component: PendingBookingsComponent,
      },
      { path: 'history', component: HistoryBookingsComponent },
    ],
  },
];
