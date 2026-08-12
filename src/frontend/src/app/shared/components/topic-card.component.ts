import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { APP_COPY } from '../../core/i18n/app-copy.constants';
import type { Topic } from '../../public/shared/topic.constants';

@Component({
  selector: 'app-topic-card',
  imports: [RouterLink],
  host: {
    class: 'topic-card-host',
    '[class]': '"topic-card-host--" + topic().accent',
    '[attr.id]': '"topic-" + topic().id',
  },
  template: `
    <article class="topic-card">
      <span class="topic-card__icon" [class]="'topic-card__icon--' + topic().accent" aria-hidden="true">
        @switch (topic().id) {
          @case ('communication') {
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
              <path
                d="M4 6a2 2 0 012-2h12a2 2 0 012 2v8a2 2 0 01-2 2H9l-4 3v-3H6a2 2 0 01-2-2V6z"
                fill="currentColor"
              />
            </svg>
          }
          @case ('trust') {
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
              <path
                d="M12 3l7 3v5c0 4.5-3 7.8-7 9-4-1.2-7-4.5-7-9V6l7-3z"
                stroke="currentColor"
                stroke-width="1.75"
                stroke-linejoin="round"
              />
            </svg>
          }
          @case ('premarital') {
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
              <circle cx="9" cy="14" r="4" stroke="currentColor" stroke-width="1.75" />
              <circle cx="15" cy="14" r="4" stroke="currentColor" stroke-width="1.75" />
            </svg>
          }
          @case ('dating-confidence') {
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
              <circle cx="9" cy="10" r="3" fill="currentColor" />
              <circle cx="15" cy="10" r="3" fill="currentColor" />
              <path
                d="M5 18c1.5-2 3.2-3 4-3s2.5 1 4 3M11 18c1.5-2 3.2-3 4-3s2.5 1 4 3"
                stroke="currentColor"
                stroke-width="1.75"
                stroke-linecap="round"
              />
            </svg>
          }
          @default {
            <svg viewBox="0 0 24 24" width="22" height="22" fill="none">
              <circle cx="12" cy="12" r="8" stroke="currentColor" stroke-width="1.75" />
              <path d="M2 12h20M12 2a14 14 0 010 20M12 2a14 14 0 000 20" stroke="currentColor" stroke-width="1.75" />
            </svg>
          }
        }
      </span>

      <h3 class="topic-card__title">{{ topic().title }}</h3>
      <p class="topic-card__description">{{ description() }}</p>

      @if (showDiscoverLink()) {
        <a class="topic-card__link" [class]="'topic-card__link--' + topic().accent" routerLink="/booking/start">
          {{ copy.cta.discoverMore }}
          <span aria-hidden="true">←</span>
        </a>
      }
    </article>
  `,
  styles: `
    .topic-card {
      display: grid;
      gap: var(--space-md);
      height: 100%;
      padding: var(--space-xl);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-card);
      transition:
        box-shadow var(--transition-base),
        transform var(--transition-base);
    }

    .topic-card:hover {
      box-shadow: var(--shadow-card-hover);
      transform: translateY(-2px);
    }

    .topic-card__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 3rem;
      height: 3rem;
      border-radius: var(--radius-full);
    }

    .topic-card__icon--purple {
      background: var(--color-topic-purple-bg);
      color: var(--color-topic-purple);
    }

    .topic-card__icon--green {
      background: var(--color-topic-green-bg);
      color: var(--color-topic-green);
    }

    .topic-card__icon--orange {
      background: var(--color-topic-orange-bg);
      color: var(--color-topic-orange);
    }

    .topic-card__icon--pink {
      background: var(--color-topic-pink-bg);
      color: var(--color-topic-pink);
    }

    .topic-card__icon--sky {
      background: var(--color-topic-sky-bg);
      color: var(--color-topic-sky);
    }

    .topic-card__title {
      margin: 0;
      font-size: var(--font-size-lg);
      font-weight: 700;
    }

    .topic-card__description {
      margin: 0;
      flex: 1;
      color: var(--color-text-muted);
      font-size: var(--font-size-sm);
      line-height: var(--line-height-body);
    }

    .topic-card__link {
      display: inline-flex;
      align-items: center;
      gap: var(--space-xs);
      width: fit-content;
      font-size: var(--font-size-sm);
      font-weight: 600;
      text-decoration: none;
      transition: gap var(--transition-fast);
    }

    .topic-card__link:hover {
      gap: var(--space-sm);
    }

    .topic-card__link--purple {
      color: var(--color-topic-purple);
    }

    .topic-card__link--green {
      color: var(--color-topic-green);
    }

    .topic-card__link--orange {
      color: var(--color-topic-orange);
    }

    .topic-card__link--pink {
      color: var(--color-topic-pink);
    }

    .topic-card__link--sky {
      color: var(--color-topic-sky);
    }
  `,
})
export class TopicCardComponent {
  readonly topic = input.required<Topic>();
  readonly description = input.required<string>();
  readonly showDiscoverLink = input(false);

  protected readonly copy = APP_COPY;
}
