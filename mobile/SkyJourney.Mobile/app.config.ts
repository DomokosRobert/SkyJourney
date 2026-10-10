import type { ConfigContext, ExpoConfig } from 'expo/config';

/**
 * Build-time configuration layered over app.json. Account-specific values are NOT committed: they come from the
 * environment (EAS environment variables, or your shell for local builds). See docs/deployment.md.
 *
 * - APP_VARIANT: "development" | "preview" | "production" (set per profile in eas.json). Non-production variants get
 *   their own identifier and name so they can be installed next to the store build.
 * - APP_IOS_BUNDLE_ID / APP_ANDROID_PACKAGE: the identifiers you register with Apple and Google. The fallback
 *   "com.example.skyjourney" is a placeholder and cannot be submitted to either store.
 * - EAS_PROJECT_ID / EXPO_OWNER: written by `eas init` for your Expo account.
 */
const PLACEHOLDER_ID = 'com.example.skyjourney';

export default ({ config }: ConfigContext): ExpoConfig => {
  const variant = process.env.APP_VARIANT ?? 'development';
  const suffix = variant === 'production' ? '' : `.${variant}`;
  const iosBundleId = (process.env.APP_IOS_BUNDLE_ID ?? PLACEHOLDER_ID) + suffix;
  const androidPackage = (process.env.APP_ANDROID_PACKAGE ?? PLACEHOLDER_ID) + suffix;
  const projectId = process.env.EAS_PROJECT_ID;

  return {
    ...config,
    name: variant === 'production' ? 'SkyJourney' : `SkyJourney (${variant})`,
    slug: config.slug ?? 'skyjourney',
    ...(process.env.EXPO_OWNER ? { owner: process.env.EXPO_OWNER } : {}),
    ios: {
      ...config.ios,
      bundleIdentifier: iosBundleId,
      // The app only uses HTTPS/TLS provided by the OS (exempt encryption). Confirm this for your own submission.
      config: { ...config.ios?.config, usesNonExemptEncryption: false },
    },
    android: {
      ...config.android,
      package: androidPackage,
    },
    extra: {
      ...config.extra,
      appVariant: variant,
      ...(projectId ? { eas: { projectId } } : {}),
    },
  };
};
