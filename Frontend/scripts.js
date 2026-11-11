const apiAuthBase = '/api/auth';
const apiDashboardBase = '/api/dashboard';
const apiZohoBase = '/api/zoho';

const faqAccordions = document.querySelectorAll('.faq-item');
faqAccordions.forEach(item => {
  item.addEventListener('click', () => {
    const content = item.querySelector('.faq-answer');
    const icon = item.querySelector('.faq-toggle');
    const isOpen = content.classList.toggle('d-none');
    icon.textContent = isOpen ? '+' : '-';
  });
});

const getAuthToken = () => localStorage.getItem('authToken');

const parseJwt = (token) => {
  try {
    const payload = token.split('.')[1];
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/');
    const json = atob(base64);
    try { return JSON.parse(decodeURIComponent(escape(json))); } catch { return JSON.parse(json); }
  } catch { return null; }
};

const revealAdminFromToken = () => {
  const t = getAuthToken();
  console.log('Auth token:', t);
  if (!t) return;
  const decoded = parseJwt(t);
  const role = decoded?.role || decoded?.["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || decoded?.["roles"];
  console.log('Decoded JWT role:', role);
  if (role === 'Admin') document.getElementById('adminPortalLink')?.classList.remove('d-none');
};

const fetchDashboardSummary = async () => {
  const authToken = getAuthToken();
  if (!authToken) {
    window.location.href = 'login.html';
    return;
  }

  try {
    const response = await fetch(`${apiDashboardBase}/summary`, {
      headers: {
        Authorization: `Bearer ${authToken}`
      }
    });
    if (!response.ok) {
      localStorage.removeItem('authToken');
      window.location.href = 'login.html';
      return;
    }

    const data = await response.json();
    document.getElementById('currentPlan').textContent = `${data.activePlan} Plan`;
    document.getElementById('subscriptionStatus').textContent = data.subscriptionStatus;
    document.getElementById('expiryDate').textContent = data.expiryDate;
    document.getElementById('remainingDays').textContent = `${data.remainingDays} days`;
    document.getElementById('lastLogin').textContent = data.user.lastLogin;
    document.getElementById('userName').textContent = data.user.fullName || data.user.email || 'User';
    document.getElementById('cardPlan').textContent = data.activePlan;
    document.getElementById('cardStatus').textContent = data.subscriptionStatus;
    document.getElementById('cardExpiry').textContent = data.expiryDate;
    document.getElementById('cardLastLogin').textContent = data.user.lastLogin;
    document.getElementById('zohoBaseUrl').innerHTML = `${data.zohoConfig.baseUrl || 'https://www.zohoapis.com/books/v3'} <span class="text-muted">(readonly default)</span>`;
    document.getElementById('zohoOrganizationId').textContent = data.zohoConfig.organizationId || 'Not configured';
    document.getElementById('zohoClientId').textContent = data.zohoConfig.clientId || 'Not configured';
    document.getElementById('zohoStatus').textContent = data.zohoConfig.status;
    document.getElementById('zohoLastValidated').textContent = data.zohoConfig.lastValidated;

    if (data.user?.role === 'Admin') {
      document.getElementById('adminPortalLink')?.classList.remove('d-none');
    }

    const baseUrlInput = document.getElementById('zohoBaseUrlInput');
    if (baseUrlInput) {
      baseUrlInput.value = data.zohoConfig.baseUrl || '';
    }
    const orgIdInput = document.getElementById('zohoOrganizationIdInput');
    if (orgIdInput) {
      orgIdInput.value = data.zohoConfig.organizationId || '';
    }
    const clientIdInput = document.getElementById('zohoClientIdInput');
    if (clientIdInput) {
      clientIdInput.value = data.zohoConfig.clientId || '';
    }
  } catch (error) {
    console.error('Dashboard fetch failed', error);
  }
};

const dashboardSummary = document.getElementById('dashboardSummary');
if (dashboardSummary) {
  revealAdminFromToken();
  fetchDashboardSummary();
  fetchZohoConfig();
}

async function fetchZohoConfig() {
  const authToken = getAuthToken();
  if (!authToken) {
    return;
  }

  try {
    const response = await fetch(`${apiZohoBase}/config`, {
      headers: {
        Authorization: `Bearer ${authToken}`
      }
    });

    if (!response.ok) {
      return;
    }

    const data = await response.json();
    const baseUrlInput = document.getElementById('zohoBaseUrlInput');
    if (baseUrlInput) {
      baseUrlInput.value = data.baseUrl || 'https://www.zohoapis.com/books/v3';
    }

    const orgIdInput = document.getElementById('zohoOrganizationIdInput');
    if (orgIdInput) {
      orgIdInput.value = data.organizationId || '';
    }

    const clientIdInput = document.getElementById('zohoClientIdInput');
    if (clientIdInput) {
      clientIdInput.value = data.clientId || '';
    }

    const clientSecretInput = document.getElementById('zohoClientSecretInput');
    if (clientSecretInput) {
      clientSecretInput.value = data.clientSecret || '';
    }

    const refreshTokenInput = document.getElementById('zohoRefreshTokenInput');
    if (refreshTokenInput) {
      refreshTokenInput.value = data.refreshToken || '';
    }
  } catch (error) {
    console.error('Zoho config fetch failed', error);
  }
};

const showAuthAlert = (element, message, type = 'success') => {
  if (!element) return;
  element.textContent = message;
  element.classList.remove('d-none', 'alert-success', 'alert-danger', 'alert-warning');
  element.classList.add('alert', type === 'success' ? 'alert-success' : type === 'warning' ? 'alert-warning' : 'alert-danger');
};

const loginForm = document.getElementById('loginForm');
if (loginForm) {
  loginForm.addEventListener('submit', async event => {
    event.preventDefault();
    const alertBox = document.getElementById('authAlert');
    const email = document.getElementById('loginEmail').value.trim();
    const password = document.getElementById('loginPassword').value.trim();

    if (!email || !password) {
      showAuthAlert(alertBox, 'Email and password are required.', 'danger');
      return;
    }

    if (window.location.protocol === 'file:') {
      showAuthAlert(alertBox, 'Please open the login page through the backend server (HTTP), not the local file system.', 'danger');
      return;
    }

    try {
      const response = await fetch(`${apiAuthBase}/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
      });

      let body = null;
      const contentType = response.headers.get('content-type') || '';
      if (contentType.includes('application/json')) {
        try {
          body = await response.json();
        } catch (jsonError) {
          console.error('Login JSON parse failed', jsonError);
        }
      }

      if (!response.ok) {
        const message = body?.error || body?.message || `Login failed with status ${response.status}.`;
        showAuthAlert(alertBox, message, 'danger');
        return;
      }

      if (!body || !body.token) {
        showAuthAlert(alertBox, 'Login succeeded but server did not return a token.', 'danger');
        return;
      }

      localStorage.setItem('authToken', body.token);
      showAuthAlert(alertBox, 'Login successful! Redirecting...', 'success');
      setTimeout(() => window.location.href = 'dashboard.html', 800);
    } catch (error) {
      console.error('Login request failed', error);
      showAuthAlert(alertBox, 'Unable to connect to the backend server. Make sure the WebAPI is running and the page is opened via HTTP.', 'danger');
    }
  });
}

const registerForm = document.getElementById('registerForm');
if (registerForm) {
  // If the page includes OTP UI (custom register flow), skip the legacy handler
  if (document.getElementById('otpArea')) {
    console.log('Custom register flow detected, skipping legacy register handler');
  } else {
  registerForm.addEventListener('submit', async event => {
    event.preventDefault();
    const alertBox = document.getElementById('authAlert');
    const fullName = document.getElementById('registerFullName').value.trim();
    const email = document.getElementById('registerEmail').value.trim();
    const password = document.getElementById('registerPassword').value.trim();
    const confirmPassword = document.getElementById('registerConfirmPassword').value.trim();

    if (!fullName || !email || !password || !confirmPassword) {
      showAuthAlert(alertBox, 'All fields are required.', 'danger');
      return;
    }

    if (password !== confirmPassword) {
      showAuthAlert(alertBox, 'Passwords do not match.', 'danger');
      return;
    }

    try {
      const response = await fetch(`${apiAuthBase}/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ fullName, email, password })
      });

      const body = await response.json();
      if (!response.ok) {
        showAuthAlert(alertBox, body.error || 'Unable to register.', 'danger');
        return;
      }

      showAuthAlert(alertBox, 'Registration successful! Redirecting to login...', 'success');
      setTimeout(() => window.location.href = 'login.html', 1200);
    } catch (error) {
      showAuthAlert(alertBox, 'Unable to connect to the authentication service.', 'danger');
    }
  });
  }
}

const forgotForm = document.getElementById('forgotForm');
if (forgotForm) {
  forgotForm.addEventListener('submit', async event => {
    event.preventDefault();
    const alertBox = document.getElementById('authAlert');
    const email = document.getElementById('forgotEmail').value.trim();

    if (!email) {
      showAuthAlert(alertBox, 'Please enter your email address.', 'danger');
      return;
    }

    try {
      const response = await fetch(`${apiAuthBase}/forgot-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email })
      });

      const body = await response.json();
      if (!response.ok) {
        showAuthAlert(alertBox, body.error || 'Unable to submit reset request.', 'danger');
        return;
      }

      const message = body.resetToken ? `Reset token generated: ${body.resetToken}. Use it below to complete your reset.` : 'If that account exists, reset instructions were sent.';
      showAuthAlert(alertBox, message, 'success');
    } catch (error) {
      showAuthAlert(alertBox, 'Unable to connect to the authentication service.', 'danger');
    }
  });
}

const resetForm = document.getElementById('resetForm');
if (resetForm) {
  resetForm.addEventListener('submit', async event => {
    event.preventDefault();
    const alertBox = document.getElementById('authAlert');
    const email = document.getElementById('resetEmail').value.trim();
    const token = document.getElementById('resetToken').value.trim();
    const newPassword = document.getElementById('resetPassword').value.trim();

    if (!email || !token || !newPassword) {
      showAuthAlert(alertBox, 'All fields are required to reset your password.', 'danger');
      return;
    }

    try {
      const response = await fetch(`${apiAuthBase}/reset-password`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, token, newPassword })
      });

      const body = await response.json();
      if (!response.ok) {
        showAuthAlert(alertBox, body.error || 'Unable to reset password.', 'danger');
        return;
      }

      showAuthAlert(alertBox, 'Password reset successful! You can now login.', 'success');
      setTimeout(() => window.location.href = 'login.html', 1200);
    } catch (error) {
      showAuthAlert(alertBox, 'Unable to connect to the authentication service.', 'danger');
    }
  });
}

const contactForm = document.getElementById('contactForm');
const contactAlert = document.getElementById('contactAlert');
if (contactForm) {
  contactForm.addEventListener('submit', async (event) => {
    event.preventDefault();
    contactAlert.classList.remove('d-none', 'alert-danger', 'alert-success');
    contactAlert.textContent = '';

    const name = document.getElementById('contactName').value.trim();
    const email = document.getElementById('contactEmail').value.trim();
    const message = document.getElementById('contactMessage').value.trim();

    if (!name || !email || !message) {
      contactAlert.textContent = 'Please fill in all fields before submitting.';
      contactAlert.classList.add('alert-danger');
      return;
    }

    const isFileProtocol = window.location.protocol === 'file:';
    const apiUrl = isFileProtocol
      ? 'http://localhost:5000/api/contact'
      : `${window.location.origin}/api/contact`;

    if (isFileProtocol) {
      contactAlert.textContent = 'Please open the site through the backend server instead of using file://. Use http://localhost:5000 or your configured WebAPI URL.';
      contactAlert.classList.add('alert-danger');
      return;
    }

    try {
      const response = await fetch(apiUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ name, email, message })
      });

      let body;
      try {
        body = await response.json();
      } catch (jsonError) {
        console.error('Contact API returned non-JSON response', jsonError);
        contactAlert.textContent = 'Unexpected server response. Please try again later.';
        contactAlert.classList.add('alert-danger');
        return;
      }

      if (!response.ok) {
        contactAlert.textContent = body.error || 'Unable to send your message. Please try again later.';
        contactAlert.classList.add('alert-danger');
        return;
      }

      contactAlert.textContent = body.message || 'Thanks for your message! Our support team will contact you shortly.';
      contactAlert.classList.add('alert-success');
      contactForm.reset();
    } catch (err) {
      console.error('Contact submit failed', err);
      contactAlert.textContent = 'Unable to contact the server. Please try again later.';
      contactAlert.classList.add('alert-danger');
    }
  });
}

const zohoConfigForm = document.getElementById('zohoConfigForm');
const testZohoConnectionButton = document.getElementById('testZohoConnectionButton');
const zohoConfigAlert = document.getElementById('zohoConfigAlert');

const showZohoAlert = (message, type = 'success') => {
  if (!zohoConfigAlert) return;
  zohoConfigAlert.textContent = message;
  zohoConfigAlert.classList.remove('d-none', 'alert-success', 'alert-danger', 'alert-warning');
  zohoConfigAlert.classList.add('alert', type === 'success' ? 'alert-success' : type === 'warning' ? 'alert-warning' : 'alert-danger');
};

const getZohoFormValues = () => ({
  baseUrl: document.getElementById('zohoBaseUrlInput')?.value.trim() ?? 'https://www.zohoapis.com/books/v3',
  organizationId: document.getElementById('zohoOrganizationIdInput')?.value.trim() ?? '',
  clientId: document.getElementById('zohoClientIdInput')?.value.trim() ?? '',
  clientSecret: document.getElementById('zohoClientSecretInput')?.value.trim() ?? '',
  refreshToken: document.getElementById('zohoRefreshTokenInput')?.value.trim() ?? ''
});

if (zohoConfigForm) {
  zohoConfigForm.addEventListener('submit', async event => {
    event.preventDefault();
    const { baseUrl, organizationId, clientId, clientSecret, refreshToken } = getZohoFormValues();
    const missingFields = [];
    if (!organizationId) missingFields.push('Organization ID');
    if (!clientId) missingFields.push('Client ID');
    if (!clientSecret) missingFields.push('Client Secret');
    if (!refreshToken) missingFields.push('Refresh Token');
    if (missingFields.length) {
      showZohoAlert(`Please provide: ${missingFields.join(', ')}.`, 'danger');
      return;
    }

    const authToken = getAuthToken();
    if (!authToken) {
      window.location.href = 'login.html';
      return;
    }

    try {
      const validationResponse = await fetch(`${apiZohoBase}/validate`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${authToken}`
        },
        body: JSON.stringify({ baseUrl, organizationId, clientId, clientSecret, refreshToken })
      });

      const validationBody = await validationResponse.json();
      if (!validationResponse.ok) {
        showZohoAlert(validationBody.error || 'Zoho validation failed. Please check your credentials and organization ID.', 'danger');
        return;
      }

      const saveResponse = await fetch(`${apiZohoBase}/config`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${authToken}`
        },
        body: JSON.stringify({ baseUrl, organizationId, clientId, clientSecret, refreshToken })
      });

      const saveBody = await saveResponse.json();
      if (!saveResponse.ok) {
        showZohoAlert(saveBody.error || 'Unable to save Zoho configuration.', 'danger');
        return;
      }
      console.log('Zoho save response', saveBody);

      showZohoAlert(saveBody.message || 'Zoho configuration validated and saved successfully.', 'success');
      fetchDashboardSummary();
      fetchZohoConfig();
    } catch (error) {
      showZohoAlert('Unable to connect to the Zoho service.', 'danger');
    }
  });
}

if (testZohoConnectionButton) {
  testZohoConnectionButton.addEventListener('click', async () => {
    const { baseUrl, organizationId, clientId, clientSecret, refreshToken } = getZohoFormValues();
    const missingFields = [];
    if (!organizationId) missingFields.push('Organization ID');
    if (!clientId) missingFields.push('Client ID');
    if (!clientSecret) missingFields.push('Client Secret');
    if (!refreshToken) missingFields.push('Refresh Token');
    if (missingFields.length) {
      showZohoAlert(`Please provide: ${missingFields.join(', ')}.`, 'danger');
      return;
    }
    const authToken = getAuthToken();
    if (!authToken) {
      window.location.href = 'login.html';
      return;
    }

    try {
      const response = await fetch(`${apiZohoBase}/validate`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          Authorization: `Bearer ${authToken}`
        },
        body: JSON.stringify({ baseUrl, organizationId, clientId, clientSecret, refreshToken })
      });
      const body = await response.json();
      if (!response.ok) {
        showZohoAlert(body.error || 'Zoho validation failed.', 'danger');
        return;
      }
      showZohoAlert(body.status || 'Zoho connection validated.', 'success');
      document.getElementById('zohoStatus').textContent = 'Validated';
      document.getElementById('zohoLastValidated').textContent = body.lastValidated || 'Just now';
      // Do not refresh dashboard summary here; keep the current form values so user can save after test
    } catch (error) {
      showZohoAlert('Unable to connect to the Zoho validation service.', 'danger');
    }
  });
}

// Public stats (landing page) — fetch and animate total registered count
const apiPublic = '/api/public';

function animateCount(el, to) {
  if (!el) return;
  const start = parseInt(el.textContent.replace(/[^0-9]/g, '')) || 0;
  const end = parseInt(to, 10) || 0;
  const duration = 800;
  const frameRate = 60;
  const totalFrames = Math.round((duration / 1000) * frameRate);
  let frame = 0;
  const diff = end - start;
  if (diff === 0) { el.textContent = end; return; }
  const counter = setInterval(() => {
    frame++;
    const progress = frame / totalFrames;
    const value = Math.round(start + diff * (1 - Math.pow(1 - progress, 3)));
    el.textContent = value;
    if (frame >= totalFrames) {
      clearInterval(counter);
      el.textContent = end;
    }
  }, Math.round(1000 / frameRate));
}

async function fetchPublicStats() {
  try {
    const res = await fetch(`${apiPublic}/stats`);
    if (!res.ok) return;
    const data = await res.json();
    const el = document.getElementById('totalRegisteredCount');
    if (el && typeof data.totalUsers !== 'undefined') {
      animateCount(el, data.totalUsers);
    }
  } catch (err) {
    // ignore
  }
}

// Start polling on pages that include the counter
if (document.getElementById('totalRegisteredCount')) {
  fetchPublicStats();
  setInterval(fetchPublicStats, 15000);
}

