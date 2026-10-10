import { Text, type TextProps } from 'react-native';

import { typography, type TypographyVariant } from '@/theme';

type Props = TextProps & { variant?: TypographyVariant; color?: string };

export function AppText({ variant = 'body', color, style, ...rest }: Props) {
  return <Text {...rest} style={[typography[variant], color ? { color } : null, style]} />;
}
