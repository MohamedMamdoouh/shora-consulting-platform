import { Routes } from '@angular/router';
import { PlaceholderPageComponent } from '../shared/components/placeholder-page.component';

export const CLIENT_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    component: PlaceholderPageComponent,
    data: { title: 'لوحة العميل', message: 'لوحة العميل — المواصفة 06.' }
  }
];
