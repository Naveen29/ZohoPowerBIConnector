const apiAuth = '/api/auth';

const el = id => document.getElementById(id);

const showAlert = (id, message, type='success') => {
  const a = el(id);
  if (!a) return;
  a.textContent = message;
  a.classList.remove('d-none','alert-success','alert-danger');
  a.classList.add('alert', type==='success' ? 'alert-success' : 'alert-danger');
};

const hideAlert = (id) => {
  const a = el(id);
  if (!a) return;
  a.classList.add('d-none');
};

const showOtpArea = (email) => {
  el('otpArea').classList.remove('d-none');
  el('registerForm').querySelectorAll('input,button').forEach(i=>i.disabled=true);
  el('otpInput').focus();
};

const registerHandler = async (e) => {
  e.preventDefault();
  hideAlert('authAlert');
  const fullName = el('registerFullName').value.trim();
  const email = el('registerEmail').value.trim();
  const password = el('registerPassword').value;
  const confirm = el('registerConfirmPassword').value;
  if (!fullName || !email || !password) { showAlert('authAlert','Please fill required fields','danger'); return; }
  if (password !== confirm) { showAlert('authAlert','Passwords do not match','danger'); return; }

  try {
    const res = await fetch(`${apiAuth}/register`,{
      method:'POST',
      headers:{'Content-Type':'application/json'},
      body: JSON.stringify({ fullName, email, password })
    });
    const body = await res.json();
    if (!res.ok) { showAlert('authAlert', body.error || 'Registration failed', 'danger'); return; }
    showAlert('authAlert', body.message || 'OTP sent to email', 'success');
    showOtpArea(email);
  } catch (err) {
    console.error('register error', err);
    showAlert('authAlert','Unable to contact server','danger');
  }
};

const verifyOtpHandler = async (e) => {
  e.preventDefault();
  hideAlert('otpAlert');
  const email = el('registerEmail').value.trim();
  const otp = el('otpInput').value.trim();
  if (!otp) { showAlert('otpAlert','Enter the OTP','danger'); return; }
  try {
    const res = await fetch(`${apiAuth}/verify-email`,{
      method:'POST',
      headers:{'Content-Type':'application/json'},
      body: JSON.stringify({ email, otp })
    });
    const body = await res.json();
    if (!res.ok) { showAlert('otpAlert', body.error || 'Verification failed', 'danger'); return; }
    showAlert('otpAlert', body.message || 'Email verified. Redirecting to login...', 'success');
    setTimeout(()=>{ window.location.href = 'login.html'; }, 1300);
  } catch (err) {
    console.error('verify error', err);
    showAlert('otpAlert','Unable to contact server','danger');
  }
};

const resendOtpHandler = async (e) => {
  e.preventDefault();
  hideAlert('otpAlert');
  const email = el('registerEmail').value.trim();
  try {
    const res = await fetch(`${apiAuth}/resend-otp`,{
      method:'POST',
      headers:{'Content-Type':'application/json'},
      body: JSON.stringify({ email })
    });
    const body = await res.json();
    if (!res.ok) { showAlert('otpAlert', body.error || 'Resend failed', 'danger'); return; }
    showAlert('otpAlert', body.message || 'OTP resent to email', 'success');
  } catch (err) {
    console.error('resend error', err);
    showAlert('otpAlert','Unable to contact server','danger');
  }
};

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', ()=>{
    el('registerForm')?.addEventListener('submit', registerHandler);
    el('verifyOtpBtn')?.addEventListener('click', verifyOtpHandler);
    el('resendOtpBtn')?.addEventListener('click', resendOtpHandler);
  });
} else {
  el('registerForm')?.addEventListener('submit', registerHandler);
  el('verifyOtpBtn')?.addEventListener('click', verifyOtpHandler);
  el('resendOtpBtn')?.addEventListener('click', resendOtpHandler);
}
