import { UserManager, WebStorageStateStore, type User } from 'oidc-client-ts';

// Replace 'your-client-id' with the client ID registered in QuantumID.
// The redirect URIs below must match exactly what you configured in the
// QuantumID dashboard — trailing slashes matter.
export const userManager = new UserManager({
  authority: 'https://id.quantumapi.eu',
  client_id: 'your-client-id',
  redirect_uri: `${window.location.origin}/signin-oidc`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  scope: 'openid profile email',
  response_type: 'code',

  // Store the session in localStorage so it survives page refreshes.
  // Use sessionStorage if you prefer tokens to die with the tab.
  userStore: new WebStorageStateStore({ store: window.localStorage }),

  // Automatically renew the access token 60 seconds before it expires.
  automaticSilentRenew: true,
  silent_redirect_uri: `${window.location.origin}/silent-renew.html`,
});

/** Redirect the browser to the QuantumID login page. */
export const login = (): Promise<void> =>
  userManager.signinRedirect();

/**
 * Call this on the /signin-oidc page to complete the PKCE handshake
 * and store the tokens. Then redirect the user back to where they were.
 */
export const handleCallback = (): Promise<User> =>
  userManager.signinRedirectCallback();

/** Redirect to QuantumID to clear the session there too. */
export const logout = (): Promise<void> =>
  userManager.signoutRedirect();

/**
 * Returns the current access token, or null if the user is not signed in.
 * The token is ready to use as a Bearer header value.
 *
 * Example:
 *   const token = await getAccessToken();
 *   fetch('/api/v1/users/me', {
 *     headers: { Authorization: `Bearer ${token}` }
 *   });
 */
export const getAccessToken = async (): Promise<string | null> => {
  const user = await userManager.getUser();
  if (!user || user.expired) {
    return null;
  }
  return user.access_token;
};

/**
 * Returns the currently signed-in user profile, or null if not signed in.
 */
export const getCurrentUser = async (): Promise<User | null> => {
  const user = await userManager.getUser();
  if (!user || user.expired) {
    return null;
  }
  return user;
};
