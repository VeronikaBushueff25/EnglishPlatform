import { AbstractControl, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';

/** Те же правила, что у Identity на сервере: от 8 символов, есть цифра и строчная буква. */
export const PASSWORD_HINT = 'Не меньше 8 символов, хотя бы одна цифра и одна строчная буква.';

export const passwordValidators: ValidatorFn[] = [
  Validators.required,
  Validators.minLength(8),
  Validators.maxLength(128),
  (control: AbstractControl): ValidationErrors | null => {
    const value = String(control.value ?? '');
    if (value === '') {
      return null;
    }
    const hasDigit = /\d/.test(value);
    const hasLower = /\p{Ll}/u.test(value);
    return hasDigit && hasLower ? null : { passwordRules: true };
  },
];

/** Проверка группы: поле confirm совпадает с полем password. */
export const passwordsMatch: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const password = group.get('password')?.value;
  const confirm = group.get('confirm')?.value;
  return confirm && password !== confirm ? { passwordsMismatch: true } : null;
};
