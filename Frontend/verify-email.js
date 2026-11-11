const apiAuth = '/api/auth';

const el = id => document.getElementById(id);

const showVerifyAlert = (message, type = 'success') => {
  const box = el('verifyAlert');
  if (!box) return;
  box.textContent = message;
  box.classList.remove('d-none', 'alert-success', 'alert-danger');
  box.classList.add('alert', type === 'success' ? 'alert-success' : 'alert-danger');
};

const hideVerifyAlert = () => {
  const box = el('verifyAlert');
  if (!box) return;
  box.classList.add('d-none');
};

const verifyForm = el('verifyForm');
const resendButton = el('resendOtpButton');

if (verifyForm) {
  verifyForm.addEventListener('submit', async event => {
    event.preventDefault();
    hideVerifyAlert();

    const email = el('verifyEmail').value.trim();
    const otp = el('verifyOtp').value.trim();
    if (!email || !otp) {
      showVerifyAlert('Email and OTP are required.', 'danger');
      return;
    }

    try {
      const response = await fetch(`${apiAuth}/verify-email`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, otp })
      });
      const body = await response.json();
      if (!response.ok) {
        showVerifyAlert(body.error || 'Verification failed.', 'danger');
        return;
      }
      showVerifyAlert(body.message || 'Email verified successfully. Redirecting to login...', 'success');
      setTimeout(() => { window.location.href = 'login.html'; }, 1400);
    } catch (error) {
      console.error('Verify email failed', error);
      showVerifyAlert('Unable to connect to the server. Please try again.', 'danger');
    }
  });
}

if (resendButton) {
  resendButton.addEventListener('click', async () => {
    hideVerifyAlert();
    const email = el('verifyEmail').value.trim();
    if (!email) {
      showVerifyAlert('Enter your email to resend OTP.', 'danger');
      return;
    }

    try {
      const response = await fetch(`${apiAuth}/resend-otp`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email })
      });
      const body = await response.json();
      if (!response.ok) {
        showVerifyAlert(body.error || 'Unable to resend OTP.', 'danger');
        return;
      }
      showVerifyAlert(body.message || 'OTP resent successfully.', 'success');
    } catch (error) {
      console.error('Resend OTP failed', error);
      showVerifyAlert('Unable to connect to the server. Please try again.', 'danger');
    }
  });
}
