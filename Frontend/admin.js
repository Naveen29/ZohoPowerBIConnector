const apiAdmin = '/api/admin';
const apiPlans = '/api/plans';
const apiZoho = '/api/zoho';
const token = localStorage.getItem('authToken');

console.log('Admin page loaded. Token exists:', !!token);

const adminUsersState = {
  page: 1,
  pageSize: 10,
  totalCount: 0,
  search: ''
};

const showAdminAlert = (message, type='success') => {
  const el = document.getElementById('adminUsersAlert');
  if (!el) return;
  el.textContent = message;
  el.classList.remove('d-none','alert-success','alert-danger');
  el.classList.add('alert', type==='success' ? 'alert-success' : 'alert-danger');
};

const renderAdminUsersPagination = (totalPages) => {
  const container = document.getElementById('adminUsersPagination');
  if (!container) return;
  container.innerHTML = '';
  if (totalPages <= 1) return;

  const nav = document.createElement('nav');
  nav.innerHTML = `
    <ul class="pagination justify-content-center mb-0">
      <li class="page-item ${adminUsersState.page <= 1 ? 'disabled' : ''}">
        <button class="page-link" type="button" id="adminUsersPrev">Previous</button>
      </li>
      <li class="page-item disabled">
        <span class="page-link">Page ${adminUsersState.page} of ${totalPages}</span>
      </li>
      <li class="page-item ${adminUsersState.page >= totalPages ? 'disabled' : ''}">
        <button class="page-link" type="button" id="adminUsersNext">Next</button>
      </li>
    </ul>
  `;
  container.appendChild(nav);

  document.getElementById('adminUsersPrev')?.addEventListener('click', () => {
    if (adminUsersState.page > 1) {
      fetchUsers(adminUsersState.search, adminUsersState.page - 1);
    }
  });

  document.getElementById('adminUsersNext')?.addEventListener('click', () => {
    if (adminUsersState.page < totalPages) {
      fetchUsers(adminUsersState.search, adminUsersState.page + 1);
    }
  });
};

const fetchUsers = async (search='', page=1) => {
  try {
    adminUsersState.search = search;
    adminUsersState.page = page;

    const params = new URLSearchParams({
      page: page.toString(),
      pageSize: adminUsersState.pageSize.toString()
    });

    if (search) {
      params.set('search', search);
    }

    const url = `${apiAdmin}/users?${params.toString()}`;
    console.log('Fetching users from:', url);
    const res = await fetch(url, { headers: { Authorization: `Bearer ${token}` } });
    console.log('Users response status:', res.status);
    if (!res.ok) {
      const body = await res.json().catch(() => ({}));
      showAdminAlert(body.error || `Unable to fetch users (${res.status})`, 'danger');
      return;
    }

    const data = await res.json();
    const list = Array.isArray(data) ? data : data.items ?? [];
    adminUsersState.totalCount = data.totalCount ?? list.length;
    adminUsersState.page = data.page ?? page;
    adminUsersState.pageSize = data.pageSize ?? adminUsersState.pageSize;

    console.log('Users loaded:', list.length, 'total:', adminUsersState.totalCount);
    const tbody = document.getElementById('adminUsersTable');
    if (!tbody) {
      console.error('adminUsersTable element not found');
      return;
    }

    tbody.innerHTML = '';
    if (!list.length) {
      tbody.innerHTML = '<tr><td colspan="5" class="text-center">No users found.</td></tr>';
    } else {
      list.forEach(u => {
        const userId = u.id || u.Id;
        const email = u.email || u.Email;
        const fullName = u.fullName || u.FullName;
        const isActive = u.isActive ?? u.IsActive;
        const tr = document.createElement('tr');
        tr.innerHTML = `
          <td>${userId}</td>
          <td>${email}</td>
          <td>${fullName}</td>
          <td>${isActive ? 'Yes' : 'No'}</td>
          <td>
            <button class="btn btn-sm btn-${isActive ? 'warning' : 'success'} me-2" data-action="toggle" data-id="${userId}">${isActive ? 'Disable' : 'Enable'}</button>
            <button class="btn btn-sm btn-info me-2" data-action="assign" data-id="${userId}">Assign Subscription</button>
          </td>
        `;
        tbody.appendChild(tr);
      });
    }

    const totalPages = Math.max(1, Math.ceil(adminUsersState.totalCount / adminUsersState.pageSize));
    renderAdminUsersPagination(totalPages);

    // update info text
    const infoEl = document.getElementById('adminUsersInfo');
    if (infoEl) {
      const start = adminUsersState.totalCount === 0 ? 0 : (adminUsersState.page - 1) * adminUsersState.pageSize + 1;
      const end = Math.min(adminUsersState.totalCount, adminUsersState.page * adminUsersState.pageSize);
      infoEl.textContent = `Showing ${start}${end} of ${adminUsersState.totalCount}`.replace('\u0012', '–');
    }

    // page size selector
    const pageSizeSelect = document.getElementById('adminUsersPageSize');
    if (pageSizeSelect) {
      pageSizeSelect.value = adminUsersState.pageSize.toString();
      pageSizeSelect.onchange = () => {
        const v = parseInt(pageSizeSelect.value, 10) || 10;
        adminUsersState.pageSize = v;
        fetchUsers(adminUsersState.search, 1);
      };
    }

    document.querySelectorAll('[data-action="toggle"]').forEach(btn => {
      btn.addEventListener('click', async () => {
        const id = btn.getAttribute('data-id');
        const action = btn.textContent.trim().toLowerCase();
        const endpoint = `${apiAdmin}/users/${id}/${action === 'disable' ? 'disable' : 'enable'}`;
        try {
          const res = await fetch(endpoint, { method: 'POST', headers: { Authorization: `Bearer ${token}` } });
          const body = await res.json();
          if (!res.ok) { showAdminAlert(body.error || 'Action failed', 'danger'); return; }
          showAdminAlert(body.message || 'Action completed', 'success');
          fetchUsers(adminUsersState.search, adminUsersState.page);
        } catch (err) {
          console.error('Toggle error:', err);
          showAdminAlert('Unable to contact server', 'danger');
        }
      });
    });

    document.querySelectorAll('[data-action="assign"]')?.forEach(btn => {
      btn.addEventListener('click', async () => {
        const id = btn.getAttribute('data-id');
        try {
          const plans = await fetchPlans();
          if (!plans.length) {
            showAdminAlert('Unable to load plans for assignment.', 'danger');
            return;
          }
          const opt = plans.map(p => `${p.id}: ${p.name} (${p.durationDays} days)`).join('\n');
          const choice = prompt('Select plan id to assign:\n' + opt);
          const planId = parseInt(choice);
          if (isNaN(planId)) return;
          const res = await fetch(`${apiAdmin}/users/${id}/subscriptions/assign`, {
            method: 'POST',
            headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
            body: JSON.stringify({ planId })
          });
          const body = await res.json();
          if (!res.ok) { showAdminAlert(body.error || 'Assign failed', 'danger'); return; }
          showAdminAlert(body.message || 'Assigned', 'success');
          fetchUsers(adminUsersState.search, adminUsersState.page);
          fetchSubscriptions();
          fetchStats();
        } catch (err) {
          console.error('Assign error:', err);
          showAdminAlert('Unable to contact server', 'danger');
        }
      });
    });

  } catch (err) {
    console.error('fetchUsers error:', err);
    showAdminAlert('Unable to load users', 'danger');
  }
};

const fetchPlans = async () => {
  try {
    console.log('Fetching plans...');
    const res = await fetch(apiPlans, { headers: { Authorization: `Bearer ${token}` } });
    if (!res.ok) {
      console.warn('Plans fetch failed:', res.status);
      return [];
    }
    const plans = await res.json();
    console.log('Plans loaded:', plans.length);
    return plans;
  } catch (err) {
    console.error('fetchPlans error:', err);
    return [];
  }
};

const fetchStats = async () => {
  try {
    console.log('Fetching stats...');
    const res = await fetch(`${apiAdmin}/stats`, { headers: { Authorization: `Bearer ${token}` } });
    if (!res.ok) {
      console.warn('Stats fetch failed:', res.status);
      return;
    }
    const data = await res.json();
    console.log('Stats loaded:', data);
    document.getElementById('statTotalUsers').textContent = data.totalUsers;
    document.getElementById('statActiveUsers').textContent = data.activeUsers;
    document.getElementById('statInactiveUsers').textContent = data.inactiveUsers;
    document.getElementById('statTotalSubs').textContent = data.totalSubscriptions;
    document.getElementById('statActiveSubs').textContent = data.activeSubscriptions;
    document.getElementById('statExpiredSubs').textContent = data.expiredSubscriptions;
  } catch (err) {
    console.error('fetchStats error:', err);
  }
};

const fetchZohoConfigStatus = async () => {
  try {
    console.log('Fetching Zoho config...');
    const res = await fetch(`${apiZoho}/config`, { headers: { Authorization: `Bearer ${token}` } });
    if (!res.ok) {
      console.warn('Zoho config fetch failed:', res.status);
      return;
    }
    const config = await res.json();
    console.log('Zoho config loaded:', config);
    document.getElementById('adminZohoClientId').textContent = config.clientId || 'Not configured';
    document.getElementById('adminZohoStatus').textContent = config.status || 'Unknown';
    document.getElementById('adminZohoLastValidated').textContent = config.lastValidated || 'Never';
  } catch (err) {
    console.error('fetchZohoConfigStatus error:', err);
  }
};

const fetchSubscriptions = async () => {
  try {
    console.log('Fetching subscriptions...');
    const res = await fetch(`${apiAdmin}/subscriptions`, { headers: { Authorization: `Bearer ${token}` } });
    if (!res.ok) { 
      console.warn('Subscriptions fetch failed:', res.status);
      return; 
    }
    const list = await res.json();
    console.log('Subscriptions loaded:', list.length);
    const el = document.getElementById('adminSubscriptions');
    el.innerHTML = '';
    list.forEach(s => {
      const div = document.createElement('div');
      div.className = 'p-3 border mb-2 d-flex justify-content-between align-items-center';
      div.innerHTML = `<div><strong>User:</strong> ${s.userEmail} <strong>Plan:</strong> ${s.plan} <strong>Expiry:</strong> ${new Date(s.expiryDate).toLocaleString()}</div>
        <div>
          <button class="btn btn-sm btn-secondary me-2" data-action="extend" data-subid="${s.id}" data-userid="${s.userId}">Extend</button>
          <button class="btn btn-sm btn-danger" data-action="cancel" data-subid="${s.id}" data-userid="${s.userId}">Cancel</button>
        </div>`;
      el.appendChild(div);
    });

    document.querySelectorAll('[data-action="extend"]')?.forEach(btn => {
      btn.addEventListener('click', async () => {
        const subId = btn.getAttribute('data-subid');
        const userId = btn.getAttribute('data-userid');
        const days = parseInt(prompt('Enter number of days to extend subscription by:', '30'));
        if (isNaN(days) || days <= 0) return;
        try {
          const res = await fetch(`${apiAdmin}/users/${userId}/subscriptions/${subId}/extend`, {
            method: 'POST',
            headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
            body: JSON.stringify({ extraDays: days })
          });
          const body = await res.json();
          if (!res.ok) { showAdminAlert(body.error || 'Extend failed', 'danger'); return; }
          showAdminAlert(body.message || 'Extended', 'success');
          fetchSubscriptions();
        } catch (err) { showAdminAlert('Unable to contact server', 'danger'); }
      });
    });

    document.querySelectorAll('[data-action="cancel"]')?.forEach(btn => {
      btn.addEventListener('click', async () => {
        const subId = btn.getAttribute('data-subid');
        const userId = btn.getAttribute('data-userid');
        if (!confirm('Cancel this subscription?')) return;
        try {
          const res = await fetch(`${apiAdmin}/users/${userId}/subscriptions/${subId}/cancel`, { method: 'POST', headers: { Authorization: `Bearer ${token}` } });
          const body = await res.json();
          if (!res.ok) { showAdminAlert(body.error || 'Cancel failed', 'danger'); return; }
          showAdminAlert(body.message || 'Cancelled', 'success');
          fetchSubscriptions();
        } catch (err) { showAdminAlert('Unable to contact server', 'danger'); }
      });
    });
  } catch {}
};

const apiEmailConfig = '/api/emailconfig';
const apiContactMessages = '/api/contactmessages';

const showEmailConfigAlert = (message, type = 'success') => {
  const el = document.getElementById('emailConfigAlert');
  if (!el) return;
  el.textContent = message;
  el.classList.remove('d-none', 'alert-success', 'alert-danger');
  el.classList.add('alert', type === 'success' ? 'alert-success' : 'alert-danger');
};

const showContactMessagesAlert = (message, type = 'success') => {
  const el = document.getElementById('contactMessagesAlert');
  if (!el) return;
  el.textContent = message;
  el.classList.remove('d-none', 'alert-success', 'alert-danger');
  el.classList.add('alert', type === 'success' ? 'alert-success' : 'alert-danger');
};

const loadEmailConfig = async () => {
  try {
    console.log('Loading email config...');
    const res = await fetch(apiEmailConfig, { headers: { Authorization: `Bearer ${token}` } });
    if (!res.ok) {
      console.warn('Email config fetch failed:', res.status);
      return;
    }
    const config = await res.json();
    console.log('Email config loaded:', config);
    if (!config.exists) return;
    document.getElementById('smtpHost').value = config.smtpHost || '';
    document.getElementById('smtpPort').value = config.smtpPort || '';
    document.getElementById('smtpUsername').value = config.username || '';
    document.getElementById('smtpPassword').value = '';
    document.getElementById('enableSsl').checked = config.enableSSL || false;
    document.getElementById('fromEmail').value = config.fromEmail || '';
    document.getElementById('fromName').value = config.fromName || '';
  } catch {}
};

const saveEmailConfig = async (event) => {
  event.preventDefault();
  showEmailConfigAlert('', 'success');

  const smtpHost = document.getElementById('smtpHost').value.trim();
  const smtpPort = parseInt(document.getElementById('smtpPort').value.trim(), 10);
  const smtpUsername = document.getElementById('smtpUsername').value.trim();
  const smtpPassword = document.getElementById('smtpPassword').value.trim();
  const enableSSL = document.getElementById('enableSsl').checked;
  const fromEmail = document.getElementById('fromEmail').value.trim();
  const fromName = document.getElementById('fromName').value.trim();

  if (!smtpHost || !smtpPort || !smtpUsername || !fromEmail) {
    showEmailConfigAlert('Please fill in all required email configuration fields.', 'danger');
    return;
  }

  const payload = { smtpHost, smtpPort, username: smtpUsername, enableSSL, fromEmail, fromName };
  if (smtpPassword) payload.password = smtpPassword;

  try {
    const res = await fetch(apiEmailConfig, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });
    const body = await res.json();
    if (!res.ok) {
      showEmailConfigAlert(body.error || 'Unable to save email configuration.', 'danger');
      return;
    }
    showEmailConfigAlert(body.message || 'Email configuration saved.', 'success');
    loadEmailConfig();
  } catch (err) {
    showEmailConfigAlert('Unable to contact server.', 'danger');
  }
};

const disableEmailConfig = async () => {
  try {
    const res = await fetch(`${apiEmailConfig}/disable`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${token}` }
    });
    const body = await res.json();
    if (!res.ok) {
      showEmailConfigAlert(body.error || 'Unable to disable email configuration.', 'danger');
      return;
    }
    showEmailConfigAlert(body.message || 'Email configuration disabled.', 'success');
  } catch (err) {
    showEmailConfigAlert('Unable to contact server.', 'danger');
  }
};

const fetchContactMessages = async () => {
  try {
    console.log('Fetching contact messages...');
    const res = await fetch(apiContactMessages, { headers: { Authorization: `Bearer ${token}` } });
    if (!res.ok) {
      console.warn('Contact messages fetch failed:', res.status);
      showContactMessagesAlert('Unable to load contact messages.', 'danger');
      return;
    }
    const messages = await res.json();
    console.log('Contact messages loaded:', messages.length);
    const container = document.getElementById('contactMessagesList');
    container.innerHTML = '';
    if (!messages.length) {
      container.innerHTML = '<div class="alert alert-secondary">No contact messages yet.</div>';
      return;
    }

    messages.forEach(msg => {
      const card = document.createElement('div');
      card.className = 'card mb-3';
      card.innerHTML = `
        <div class="card-body">
          <div class="d-flex justify-content-between align-items-start mb-2">
            <div>
              <h5 class="card-title mb-1">${msg.name} <small class="text-muted">(${msg.email})</small></h5>
              <p class="card-text"><small class="text-muted">${new Date(msg.createdDate).toLocaleString()}</small></p>
            </div>
            <div>
              <button class="btn btn-sm btn-outline-primary me-2" data-action="reply" data-id="${msg.id}">Reply</button>
              <button class="btn btn-sm btn-outline-danger" data-action="delete" data-id="${msg.id}">Delete</button>
            </div>
          </div>
          <p class="card-text">${msg.message.replace(/\n/g, '<br>')}</p>
        </div>
      `;
      container.appendChild(card);
    });

    document.querySelectorAll('[data-action="reply"]').forEach(btn => {
      btn.addEventListener('click', async () => {
        const id = btn.getAttribute('data-id');
        const subject = prompt('Reply subject:', 'Re: Your message');
        if (!subject) return;
        const bodyText = prompt('Reply body:');
        if (!bodyText) return;
        try {
          const res = await fetch(`${apiContactMessages}/${id}/reply`, {
            method: 'POST',
            headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' },
            body: JSON.stringify({ subject, body: bodyText })
          });
          const result = await res.json();
          if (!res.ok) {
            showContactMessagesAlert(result.error || 'Unable to send reply.', 'danger');
            return;
          }
          showContactMessagesAlert(result.message || 'Reply sent successfully.', 'success');
        } catch (err) {
          showContactMessagesAlert('Unable to contact server.', 'danger');
        }
      });
    });

    document.querySelectorAll('[data-action="delete"]').forEach(btn => {
      btn.addEventListener('click', async () => {
        const id = btn.getAttribute('data-id');
        if (!confirm('Delete this message?')) return;
        try {
          const res = await fetch(`${apiContactMessages}/${id}`, {
            method: 'DELETE',
            headers: { Authorization: `Bearer ${token}` }
          });
          const result = await res.json();
          if (!res.ok) {
            showContactMessagesAlert(result.error || 'Unable to delete message.', 'danger');
            return;
          }
          showContactMessagesAlert(result.message || 'Message deleted.', 'success');
          fetchContactMessages();
        } catch (err) {
          showContactMessagesAlert('Unable to contact server.', 'danger');
        }
      });
    });
  } catch (err) {
    showContactMessagesAlert('Unable to load contact messages.', 'danger');
  }
};

const initAdminPage = () => {
  if (!token) {
    console.error('No auth token found. Redirecting to login...');
    alert('You must be logged in as an admin to access this page.');
    window.location.href = 'login.html';
    return;
  }

  console.log('Admin page initializing with token...');

  document.getElementById('adminSearchBtn')?.addEventListener('click', () => {
    const q = document.getElementById('adminSearch').value.trim();
    fetchUsers(q);
  });

  document.getElementById('emailConfigForm')?.addEventListener('submit', saveEmailConfig);
  document.getElementById('disableEmailConfig')?.addEventListener('click', disableEmailConfig);

  console.log('Starting data fetches...');
  fetchUsers();
  fetchSubscriptions();
  fetchStats();
  fetchZohoConfigStatus();
  loadEmailConfig();
  fetchContactMessages();
};

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initAdminPage);
} else {
  initAdminPage();
}
