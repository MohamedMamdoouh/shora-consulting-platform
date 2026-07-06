import { Routes } from '@angular/router';
import { PlaceholderPageComponent } from '../shared/components/placeholder-page.component';

export const ADMIN_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    component: PlaceholderPageComponent,
    data: { title: 'لوحة الإدارة', message: 'لوحة المستشار — المواصفة 07.' }
  }
];
