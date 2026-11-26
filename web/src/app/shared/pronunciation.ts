import { ChangeDetectionStrategy, Component, Injectable, inject, input, signal } from '@angular/core';
import { NgIcon, provideIcons } from '@ng-icons/core';
import { lucideVolume2 } from '@ng-icons/lucide';
import { HlmButtonImports } from '@spartan-ng/helm/button';

/**
 * Произношение: запись из общей базы, если у слова есть ссылка на аудио, иначе синтезатор речи браузера.
 * Голос выбирается британский или американский из установленных в системе.
 */
@Injectable({ providedIn: 'root' })
export class Pronunciation {
  readonly supported = typeof window !== 'undefined' && 'speechSynthesis' in window;

  speak(text: string, audioUrl?: string | null): void {
    if (audioUrl) {
      new Audio(audioUrl).play().catch(() => this.synthesize(text));
      return;
    }
    this.synthesize(text);
  }

  private synthesize(text: string): void {
    if (!this.supported) {
      return;
    }
    const synth = window.speechSynthesis;
    synth.cancel();
    const utterance = new SpeechSynthesisUtterance(text);
    utterance.lang = 'en-GB';
    utterance.rate = 0.9;
    const voices = synth.getVoices();
    utterance.voice =
      voices.find((v) => v.lang === 'en-GB') ??
      voices.find((v) => v.lang === 'en-US') ??
      voices.find((v) => v.lang.startsWith('en')) ??
      null;
    synth.speak(utterance);
  }
}

/** Кнопка «послушать». Клик не всплывает: кнопку ставят внутрь кликабельных карточек. */
@Component({
  selector: 'app-speak-button',
  imports: [NgIcon, HlmButtonImports],
  providers: [provideIcons({ lucideVolume2 })],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (pronunciation.supported || audioUrl()) {
      <button
        hlmBtn
        variant="ghost"
        size="icon"
        type="button"
        class="text-muted-foreground hover:text-primary shrink-0"
        [class.text-primary]="playing()"
        [attr.aria-label]="'Послушать: ' + text()"
        (click)="play($event)"
      >
        <ng-icon name="lucideVolume2" class="text-lg" />
      </button>
    }
  `,
})
export class SpeakButton {
  protected readonly pronunciation = inject(Pronunciation);
  readonly text = input.required<string>();
  readonly audioUrl = input<string | null | undefined>(null);
  protected readonly playing = signal(false);

  protected play(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.pronunciation.speak(this.text(), this.audioUrl());
    this.playing.set(true);
    setTimeout(() => this.playing.set(false), 800);
  }
}
