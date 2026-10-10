import { Controller, type Control, type FieldPath, type FieldValues } from 'react-hook-form';
import type { TextInputProps } from 'react-native';

import { TextField } from './TextField';

type Props<T extends FieldValues> = Omit<TextInputProps, 'value' | 'onChangeText' | 'onBlur'> & {
  control: Control<T>;
  name: FieldPath<T>;
  label: string;
};

/** A TextField bound to a react-hook-form field, showing that field's validation error. */
export function FormTextField<T extends FieldValues>({ control, name, label, ...inputProps }: Props<T>) {
  return (
    <Controller
      control={control}
      name={name}
      render={({ field: { value, onChange, onBlur, ref }, fieldState: { error } }) => (
        <TextField
          ref={ref}
          label={label}
          value={value ?? ''}
          onChangeText={onChange}
          onBlur={onBlur}
          error={error?.message}
          {...inputProps}
        />
      )}
    />
  );
}
