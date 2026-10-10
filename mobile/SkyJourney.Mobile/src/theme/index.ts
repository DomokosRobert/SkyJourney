export const colors = {
  primary: '#1257D6',
  primaryPressed: '#0E46AD',
  primarySoft: '#E7EFFD',
  accent: '#FF8A3D',
  background: '#F4F6FA',
  surface: '#FFFFFF',
  text: '#0F1B2D',
  textMuted: '#5B6B82',
  textOnPrimary: '#FFFFFF',
  border: '#DFE5EE',
  success: '#1E9E6A',
  successSoft: '#E5F6EE',
  danger: '#C93B3B',
  dangerSoft: '#FCEBEB',
  warning: '#A86512',
  warningSoft: '#FDF3E3',
  disabled: '#A9B4C4',
} as const;

export const spacing = { xs: 4, sm: 8, md: 12, lg: 16, xl: 24, xxl: 32 } as const;

export const radius = { sm: 8, md: 12, lg: 16, pill: 999 } as const;

export const typography = {
  title: { fontSize: 28, fontWeight: '700', color: colors.text },
  heading: { fontSize: 20, fontWeight: '700', color: colors.text },
  subheading: { fontSize: 16, fontWeight: '600', color: colors.text },
  body: { fontSize: 16, fontWeight: '400', color: colors.text },
  caption: { fontSize: 13, fontWeight: '400', color: colors.textMuted },
  label: { fontSize: 14, fontWeight: '600', color: colors.text },
} as const;

export type TypographyVariant = keyof typeof typography;
