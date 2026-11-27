import { Injectable, signal } from '@angular/core';

export type PetSignalKind = 'correct' | 'wrong' | 'finished' | 'changed';

/**
 * Связь занятия и питомца без прямой зависимости: занятие сообщает о верных/неверных ответах и окончании,
 * питомец (грузится лениво) реагирует. Здесь только сигнал — в стартовом бандле почти ничего не весит.
 */
@Injectable({ providedIn: 'root' })
export class PetSignals {
  readonly last = signal<{ kind: PetSignalKind; at: number } | null>(null);

  emit(kind: PetSignalKind): void {
    this.last.set({ kind, at: Date.now() });
  }
}
