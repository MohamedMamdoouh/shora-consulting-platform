import { Routes } from '@angular/router';
import { ShellComponent } from './shared/layout/shell.component';

export const routes: Routes = [
  {
    path: '',
    component: ShellComponent,
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./public/home/home-page.component').then((m) => m.HomePageComponent)
      },
      {
        path: 'about',
        loadComponent: () =>
          import('./public/about/about-page.component').then((m) => m.AboutPageComponent)
      },
      {
        path: 'services',
        loadComponent: () =>
          import('./public/services/services-page.component').then((m) => m.ServicesPageComponent)
      },
      {
        path: 'booking',
        loadChildren: () => import('./booking/booking.routes').then((m) => m.BOOKING_ROUTES)
      },
      {
        path: 'dashboard',
        loadChildren: () =>
          import('./client-dashboard/client-dashboard.routes').then((m) => m.CLIENT_DASHBOARD_ROUTES)
      },
      {
        path: 'admin',
        loadChildren: () =>
          import('./admin-dashboard/admin-dashboard.routes').then((m) => m.ADMIN_DASHBOARD_ROUTES)
      },
      {
        path: 'auth',
        loadChildren: () => import('./auth/auth.routes').then((m) => m.AUTH_ROUTES)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
