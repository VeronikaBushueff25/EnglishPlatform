import { FormControl, FormGroup, Validators } from '@angular/forms';
import { passwordValidators, passwordsMatch } from './password-rules';
import { safeReturnUrl } from './safe-return-url';

describe('password rules', () => {
  const control = (value: string) => new FormControl(value, passwordValidators);

  it('accept a password that the server accepts', () => {
    expect(control('secret123').valid).toBe(true);
    expect(control('пароль123').valid).toBe(true);
  });

  it('reject short, digit-less and lowercase-less passwords', () => {
    expect(control('abc1').valid).toBe(false);
    expect(control('abcdefghi').hasError('passwordRules')).toBe(true);
    expect(control('ABCDEFG12').hasError('passwordRules')).toBe(true);
  });

  it('check that the confirmation matches', () => {
    const group = new FormGroup(
      { password: new FormControl('secret123'), confirm: new FormControl('secret124', Validators.required) },
      { validators: passwordsMatch },
    );
    expect(group.hasError('passwordsMismatch')).toBe(true);
    group.controls.confirm.setValue('secret123');
    expect(group.valid).toBe(true);
  });
});

describe('safeReturnUrl', () => {
  it('keeps local paths', () => {
    expect(safeReturnUrl('/profile?tab=1')).toBe('/profile?tab=1');
  });

  it('rejects external and empty targets', () => {
    expect(safeReturnUrl('//evil.example')).toBe('/');
    expect(safeReturnUrl('/\\evil.example')).toBe('/');
    expect(safeReturnUrl('https://evil.example')).toBe('/');
    expect(safeReturnUrl(undefined)).toBe('/');
  });
});
