import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ImportWords } from './import-words';

describe('ImportWords', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
  });

  // Привязка маршрута к входам передаёт undefined, если в маршруте нет data.mode (/dictionary/import).
  it('renders the personal import when the route gives no mode', async () => {
    const fixture = TestBed.createComponent(ImportWords);
    fixture.componentRef.setInput('mode', undefined);
    await fixture.whenStable();

    const page: HTMLElement = fixture.nativeElement;
    expect(page.querySelector('h1')?.textContent).toContain('Импорт слов');
    expect(page.querySelector('textarea')?.getAttribute('placeholder')).toContain('слово;перевод');
    expect(page.querySelector('hlm-checkbox')).toBeNull();
  });

  it('renders the admin import with the update option', async () => {
    const fixture = TestBed.createComponent(ImportWords);
    fixture.componentRef.setInput('mode', 'admin');
    await fixture.whenStable();

    const page: HTMLElement = fixture.nativeElement;
    expect(page.querySelector('h1')?.textContent).toContain('Импорт в общую базу');
    expect(page.querySelector('hlm-checkbox')).not.toBeNull();
  });
});
