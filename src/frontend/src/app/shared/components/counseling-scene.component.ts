import { Component } from '@angular/core';

@Component({
  selector: 'app-counseling-scene',
  host: {
    class: 'counseling-scene',
    'aria-hidden': 'true',
  },
  template: `
    <div class="counseling-scene__glow"></div>
    <svg class="counseling-scene__svg" viewBox="0 0 400 360" xmlns="http://www.w3.org/2000/svg">
      <ellipse cx="200" cy="310" rx="120" ry="16" fill="rgba(122,158,142,0.08)" />
      <path
        d="M120 280 C120 220 140 190 175 185 C190 183 205 183 220 185 C255 190 275 220 275 280 L275 295 L120 295 Z"
        fill="#C4B8B0"
      />
      <path
        d="M130 200 C130 170 150 155 175 152 C200 149 220 170 220 200 L220 280 L130 280 Z"
        fill="#D4C8C0"
      />
      <rect x="155" y="295" width="90" height="12" rx="6" fill="#A89890" />
      <ellipse cx="195" cy="215" rx="18" ry="14" fill="#7A9E8E" opacity="0.85" />
      <rect x="280" y="250" width="8" height="55" fill="#8B6B5A" />
      <rect x="268" y="245" width="32" height="8" rx="3" fill="#A07868" />
      <ellipse cx="284" cy="242" rx="10" ry="4" fill="#B8956A" opacity="0.5" />
      <path
        d="M276 228 L276 240 Q284 244 292 240 L292 228 Z"
        fill="#fff"
        stroke="#E0D0C0"
        stroke-width="1"
      />
      <rect x="95" y="268" width="14" height="30" rx="3" fill="#C4A882" />
      <ellipse cx="102" cy="255" rx="22" ry="18" fill="#4A7C6F" opacity="0.85" />
      <ellipse cx="92" cy="248" rx="14" ry="12" fill="#5B8A7A" opacity="0.9" />
      <ellipse cx="112" cy="250" rx="12" ry="10" fill="#1A3A3A" opacity="0.75" />
    </svg>
    <div class="counseling-scene__bubble counseling-scene__bubble--chat">
      <svg viewBox="0 0 24 24" width="20" height="20" fill="none">
        <path
          d="M4 6a2 2 0 012-2h12a2 2 0 012 2v8a2 2 0 01-2 2H9l-4 3v-3H6a2 2 0 01-2-2V6z"
          fill="#1A3A3A"
        />
      </svg>
    </div>
    <div class="counseling-scene__bubble counseling-scene__bubble--heart">
      <svg viewBox="0 0 24 24" width="18" height="18" fill="none">
        <path
          d="M12 20.5l-1.2-1.1C5.4 14.4 2 11.4 2 7.8 2 5 4.2 2.8 7 2.8c1.7 0 3.3.8 4.3 2.1C12.3 3.6 13.9 2.8 15.6 2.8 18.4 2.8 20.6 5 20.6 7.8c0 3.6-3.4 6.6-8.8 11.6L12 20.5z"
          fill="#7A9E8E"
        />
      </svg>
    </div>
  `,
  styles: `
    :host {
      position: relative;
      display: flex;
      align-items: center;
      justify-content: center;
      min-height: 16rem;
    }

    .counseling-scene__glow {
      position: absolute;
      inset: 8%;
      border-radius: 50%;
      background: var(--gradient-hero-glow);
    }

    .counseling-scene__svg {
      position: relative;
      z-index: 1;
      width: min(100%, 20rem);
      height: auto;
    }

    .counseling-scene__bubble {
      position: absolute;
      display: flex;
      align-items: center;
      justify-content: center;
      width: 2.75rem;
      height: 2.75rem;
      border-radius: var(--radius-full);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      box-shadow: var(--shadow-sm);
      z-index: 2;
    }

    .counseling-scene__bubble--chat {
      top: 6%;
      inset-inline-start: 8%;
    }

    .counseling-scene__bubble--heart {
      bottom: 22%;
      inset-inline-end: 10%;
    }
  `,
})
export class CounselingSceneComponent {}
