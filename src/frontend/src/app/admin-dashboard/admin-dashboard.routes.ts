import { Routes } from '@angular/router';
import { adminGuard } from '../core/auth/auth.guard';
import { AdminShellComponent } from './admin-shell.component';
import { AdminAvailabilityPageComponent } from './availability/admin-availability-page.component';
import { AdminBookingsPageComponent } from './bookings/admin-bookings-page.component';
import { AdminEarningsPageComponent } from './earnings/admin-earnings-page.component';
import { AdminOpsPageComponent } from './ops/admin-ops-page.component';
import { AdminSettingsPageComponent } from './settings/admin-settings-page.component';

export const ADMIN_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    canActivate: [adminGuard],
    component: AdminShellComponent,
    data: { title: 'لوحة الإدارة' },
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: 'bookings',
      },
      {
        path: 'settings',
        component: AdminSettingsPageComponent,
        data: { title: 'الإعدادات' },
      },
      {
        path: 'availability',
        component: AdminAvailabilityPageComponent,
        data: { title: 'المواعيد' },
      },
      {
        path: 'bookings',
        component: AdminBookingsPageComponent,
        data: { title: 'الحجوزات' },
      },
      {
        path: 'earnings',
        component: AdminEarningsPageComponent,
        data: { title: 'الأرباح' },
      },
      {
        path: 'ops',
        component: AdminOpsPageComponent,
        data: { title: 'تنبيهات التشغيل' },
      },
    ],
  },
];
