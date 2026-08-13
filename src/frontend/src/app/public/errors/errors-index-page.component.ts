import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ErrorCatalogEntry } from '@contracts/error-catalog';
import { firstValueFrom } from 'rxjs';
import { ErrorReferenceService } from './error-reference.service';

interface ErrorCategoryGroup {
  category: string;
  items: ErrorCatalogEntry[];
}

@Component({
  selector: 'app-errors-index-page',
  imports: [RouterLink],
  templateUrl: './errors-index-page.component.html',
  styleUrl: './errors-index-page.component.scss',
})
export class ErrorsIndexPageComponent implements OnInit {
  private readonly errorReference = inject(ErrorReferenceService);

  readonly groups = signal<ErrorCategoryGroup[]>([]);
  readonly loadError = signal('');

  async ngOnInit(): Promise<void> {
    try {
      const response = await firstValueFrom(this.errorReference.list());
      this.groups.set(groupByCategory(response.items));
    } catch {
      this.loadError.set('تعذر تحميل مرجع أكواد الأخطاء.');
    }
  }
}

function groupByCategory(items: ErrorCatalogEntry[]): ErrorCategoryGroup[] {
  const map = new Map<string, ErrorCatalogEntry[]>();

  for (const item of items) {
    const category = item.code.split('.')[0] ?? 'other';
    const list = map.get(category) ?? [];
    list.push(item);
    map.set(category, list);
  }

  return [...map.entries()]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([category, categoryItems]) => ({
      category,
      items: [...categoryItems].sort((a, b) => a.code.localeCompare(b.code)),
    }));
}
