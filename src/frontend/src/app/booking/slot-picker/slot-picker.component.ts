import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { AvailabilitySlot } from '@contracts/availability';
import { catchError, map, Observable, of, startWith, Subject, switchMap } from 'rxjs';
import { AvailabilityService } from '../../core/availability/availability.service';
import { BookingFlowStateService } from '../booking-flow-state.service';
import { formatSlotTime, groupSlotsByLocalDay, SlotDayGroup } from '../utils/slot-grouping.util';

type SlotPickerViewModel =
  | { status: 'loading'; groups: SlotDayGroup[] }
  | { status: 'ready'; groups: SlotDayGroup[] }
  | { status: 'error'; groups: SlotDayGroup[] };

const initialViewModel: SlotPickerViewModel = { status: 'loading', groups: [] };

@Component({
  selector: 'app-slot-picker',
  templateUrl: './slot-picker.component.html',
  styleUrl: './slot-picker.component.scss',
})
export class SlotPickerComponent {
  private readonly availabilityService = inject(AvailabilityService);
  private readonly bookingFlow = inject(BookingFlowStateService);
  private readonly router = inject(Router);
  private readonly reload$ = new Subject<void>();

  readonly formatSlotTime = formatSlotTime;

  private readonly viewModel$ = this.reload$.pipe(
    startWith(undefined),
    switchMap(() =>
      this.availabilityService.getAvailability().pipe(
        map((response): SlotPickerViewModel => ({
          status: 'ready',
          groups: groupSlotsByLocalDay(response.slots),
        })),
        catchError((): Observable<SlotPickerViewModel> =>
          of({ status: 'error', groups: [] as SlotDayGroup[] }),
        ),
        startWith({ status: 'loading', groups: [] as SlotDayGroup[] } as SlotPickerViewModel),
      ),
    ),
  );

  readonly viewModel = toSignal(this.viewModel$, { initialValue: initialViewModel });

  reload(): void {
    this.reload$.next();
  }

  selectSlot(slot: AvailabilitySlot): void {
    this.bookingFlow.setSlot({
      id: slot.id,
      startTimeUtc: slot.startTimeUtc,
      endTimeUtc: slot.endTimeUtc,
    });
    void this.router.navigate(['/booking/delivery']);
  }
}
