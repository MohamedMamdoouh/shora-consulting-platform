import { Routes } from '@angular/router';
import { clientGuard } from '../core/auth/auth.guard';
import { PlaceholderPageComponent } from '../shared/components/placeholder-page.component';

export const CLIENT_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    canActivate: [clientGuard],
    component: PlaceholderPageComponent,
    data: { title: 'لوحة العميل', message: 'لوحة العميل — المواصفة 06.' }
  }
];
