import { Routes } from '@angular/router';
import { adminGuard } from '../core/auth/auth.guard';
import { PlaceholderPageComponent } from '../shared/components/placeholder-page.component';

export const ADMIN_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    canActivate: [adminGuard],
    component: PlaceholderPageComponent,
    data: { title: 'لوحة الإدارة', message: 'لوحة المستشار — المواصفة 07.' }
  }
];
