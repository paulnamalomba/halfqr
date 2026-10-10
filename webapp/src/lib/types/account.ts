// Shapes returned by HalfQR.Identity and HalfQR.Billing through the /api/account proxy.

export type CredentialKind = "ApiKey" | "AccessToken";

export type CredentialStatus = "active" | "expired" | "revoked";

export type AccountUser = {
  id: string;
  email: string;
  displayName: string;
  avatarUrl: string | null;
  provider: "google" | "email" | "dev";
  createdAt: string;
  lastSignInAt: string;
};

export type ApiCredential = {
  id: string;
  kind: CredentialKind;
  name: string;
  prefix: string;
  lastFour: string;
  scopes: string[];
  createdAt: string;
  expiresAt: string | null;
  lastUsedAt: string | null;
  revokedAt: string | null;
  status: CredentialStatus;
};

export type CredentialCreated = {
  credential: ApiCredential;
  secret: string;
};

export type CredentialOptions = {
  scopes: string[];
  accessTokenLifetimesDays: number[];
  apiKeyLifetimesDays: number[];
};

export type AccountSession = {
  id: string;
  createdAt: string;
  expiresAt: string;
  lastSeenAt: string;
  userAgent: string | null;
  ipAddress: string | null;
  revokedAt: string | null;
  active: boolean;
  current: boolean;
};

export type Entitlement = {
  planId: string;
  planName: string;
  isPaid: boolean;
  activeUntil: string | null;
  maxApiKeys: number;
  maxAccessTokens: number;
  requestsPerMinute: number;
};

export type BillingPlan = {
  id: string;
  name: string;
  description: string;
  amountMwk: number;
  periodDays: number;
  maxApiKeys: number;
  maxAccessTokens: number;
  requestsPerMinute: number;
  highlighted: boolean;
  features: string[];
};

export type BillingOperator = {
  id: string;
  label: string;
  numberPrefix: string;
};

export type PaymentStatus = "Pending" | "Completed" | "Failed" | "Cancelled" | "Expired" | "NeedsReview";

export type BillingPayment = {
  id: string;
  planId: string;
  amount: number;
  currency: string;
  operator: string;
  mobileLastFour: string;
  txRef: string;
  status: PaymentStatus;
  statusMessage: string | null;
  createdAt: string;
  completedAt: string | null;
};

export type BillingOverview = {
  entitlement: Entitlement;
  plans: BillingPlan[];
  operators: BillingOperator[];
  payments: BillingPayment[];
  mockMode: boolean;
};

export type AuthConfig = {
  googleEnabled: boolean;
  googleClientId: string | null;
  emailEnabled: boolean;
  devSignInEnabled: boolean;
};

export type ProblemDetails = {
  title?: string;
  type?: string;
  status?: number;
  errors?: Record<string, string[]>;
};
