(() => {
  let chatHubConnection = null;
  let chatHubStartPromise = null;
  let activeChatConversationId = 0;
  let chatSidebarRefreshPromise = null;
  let chatThreadRefreshPromise = null;

  document.addEventListener("DOMContentLoaded", async () => {
    await loadApiPage();
    initRealtimeChat().catch(console.error);
  });

  async function loadApiPage() {
    const host = document.getElementById("api-page");
    if (!host) return;

    try {
      const response = await fetch(host.dataset.apiEndpoint, {
        headers: { "X-Requested-With": "fetch" }
      });

      if (!response.ok) {
        host.innerHTML = `<div class="status-banner">No se pudo cargar el chat.</div>`;
        return;
      }

      host.innerHTML = await response.text();
      wireChatThread(document);
    } catch (error) {
      console.error(error);
      host.innerHTML = `<div class="status-banner">No se pudo cargar el chat.</div>`;
    }
  }

  async function initRealtimeChat() {
    if (document.body?.dataset.userAuthenticated !== "true" || !window.signalR) {
      return;
    }

    if (chatHubStartPromise) {
      return chatHubStartPromise;
    }

    chatHubConnection = new window.signalR.HubConnectionBuilder()
      .withUrl("/hubs/chat")
      .withAutomaticReconnect()
      .build();

    chatHubConnection.on("ConversationUpdated", () => {
      refreshChatSidebar().catch(console.error);
    });

    chatHubConnection.on("MessageReceived", payload => {
      if (!payload?.message) return;

      if (Number(payload?.conversationId || 0) === activeChatConversationId) {
        appendChatMessage(payload.message);
        if (document.visibilityState === "visible") {
          window.setTimeout(() => {
            markActiveConversationRead().catch(() => {});
          }, 120);
        }
      }

      refreshChatSidebar().catch(console.error);
    });

    chatHubConnection.on("MessagesRead", payload => {
      applyReadReceiptUpdate(payload);
    });

    chatHubConnection.onreconnected(() => {
      openActiveConversation().catch(() => {});
    });

    chatHubStartPromise = chatHubConnection.start()
      .catch(error => {
        console.error(error);
        chatHubStartPromise = null;
      });

    await chatHubStartPromise;
  }

  function wireChatThread(root = document) {
    const app = root.querySelector("[data-chat-app]");
    if (!app) {
      activeChatConversationId = 0;
      return;
    }

    const composeForm = app.querySelector("[data-chat-compose-form]");
    const input = app.querySelector("[data-chat-compose-input]");
    const counter = app.querySelector("[data-chat-compose-counter]");
    const thread = app.querySelector("[data-chat-thread]");
    const messageList = app.querySelector("[data-chat-message-list]");
    const inbox = app.querySelector("[data-chat-inbox]");
    activeChatConversationId = Number(thread?.dataset.conversationId || 0);

    if (messageList) {
      messageList.scrollTop = messageList.scrollHeight;
    }

    if (inbox && inbox.dataset.bound !== "true") {
      inbox.dataset.bound = "true";
      inbox.addEventListener("click", async event => {
        const link = event.target.closest("[data-chat-inbox-item]");
        if (!link) return;

        event.preventDefault();
        const conversationId = Number(link.dataset.conversationId || 0);
        if (conversationId <= 0) return;

        await selectChatConversation(conversationId, { pushState: true });
      });
    }

    if (counter && input) {
      const syncCounter = () => {
        counter.textContent = `${input.value.length} / 2000`;
      };

      syncCounter();
      if (input.dataset.boundCounter !== "true") {
        input.dataset.boundCounter = "true";
        input.addEventListener("input", syncCounter);
      }
    }

    if (composeForm && input && composeForm.dataset.bound !== "true") {
      composeForm.dataset.bound = "true";
      composeForm.addEventListener("submit", async event => {
        event.preventDefault();
        const body = input.value.trim();
        if (!body || activeChatConversationId <= 0) return;

        const submitButton = composeForm.querySelector("button[type='submit']");
        submitButton?.setAttribute("disabled", "disabled");

        try {
          await initRealtimeChat();
          await openActiveConversation();
          await chatHubConnection.invoke("SendMessage", activeChatConversationId, body);
          input.value = "";
          input.dispatchEvent(new Event("input", { bubbles: true }));
        } catch (error) {
          window.alert(error?.message || "No se pudo enviar el mensaje.");
        } finally {
          submitButton?.removeAttribute("disabled");
          input.focus();
        }
      });
    }

    if (activeChatConversationId > 0) {
      openActiveConversation().catch(console.error);

      if (document.body.dataset.chatVisibilityBound !== "true") {
        document.body.dataset.chatVisibilityBound = "true";
        document.addEventListener("visibilitychange", () => {
          if (document.visibilityState === "visible") {
            markActiveConversationRead().catch(() => {});
          }
        });
      }
    }

    if (app.dataset.chatPopstateBound !== "true") {
      app.dataset.chatPopstateBound = "true";
      window.addEventListener("popstate", () => {
        const match = window.location.pathname.match(/^\/Mensajes\/(\d+)$/i);
        const conversationId = match ? Number(match[1]) : 0;
        selectChatConversation(conversationId, { pushState: false }).catch(console.error);
      });
    }
  }

  async function openActiveConversation() {
    if (!chatHubConnection || activeChatConversationId <= 0) {
      return;
    }

    if (chatHubConnection.state !== "Connected") {
      await initRealtimeChat();
      if (!chatHubConnection || chatHubConnection.state !== "Connected") {
        return;
      }
    }

    await chatHubConnection.invoke("OpenConversation", activeChatConversationId);
    await markActiveConversationRead();
  }

  async function selectChatConversation(conversationId, options = {}) {
    const safeConversationId = Math.max(0, Number(conversationId || 0));
    activeChatConversationId = safeConversationId;

    await Promise.all([
      refreshChatThread(safeConversationId),
      refreshChatSidebar(safeConversationId)
    ]);

    if (options.pushState) {
      const nextUrl = safeConversationId > 0 ? `/Mensajes/${safeConversationId}` : "/Mensajes";
      window.history.pushState({}, "", nextUrl);
    }

    await openActiveConversation();
  }

  async function markActiveConversationRead() {
    if (!chatHubConnection || activeChatConversationId <= 0 || chatHubConnection.state !== "Connected") {
      return;
    }

    await chatHubConnection.invoke("MarkConversationRead", activeChatConversationId);
  }

  async function refreshChatSidebar(conversationId = activeChatConversationId) {
    const container = document.querySelector("[data-chat-sidebar-content]");
    if (!container || chatSidebarRefreshPromise) return chatSidebarRefreshPromise;

    const safeConversationId = Math.max(0, Number(conversationId || 0));
    const url = safeConversationId > 0
      ? `/api/chat/inbox/${safeConversationId}`
      : "/api/chat/inbox";

    chatSidebarRefreshPromise = fetch(url, {
      headers: { "X-Requested-With": "fetch" }
    })
      .then(response => response.text())
      .then(html => {
        container.innerHTML = html;
        wireChatThread(document);
      })
      .finally(() => {
        chatSidebarRefreshPromise = null;
      });

    return chatSidebarRefreshPromise;
  }

  async function refreshChatThread(conversationId = activeChatConversationId) {
    const container = document.querySelector("[data-chat-thread-container]");
    if (!container || chatThreadRefreshPromise) return chatThreadRefreshPromise;

    const safeConversationId = Math.max(0, Number(conversationId || 0));
    const url = safeConversationId > 0
      ? `/api/chat/thread/${safeConversationId}`
      : "/api/chat/thread";

    chatThreadRefreshPromise = fetch(url, {
      headers: { "X-Requested-With": "fetch" }
    })
      .then(response => response.text())
      .then(html => {
        container.innerHTML = html;
        wireChatThread(document);
      })
      .finally(() => {
        chatThreadRefreshPromise = null;
      });

    return chatThreadRefreshPromise;
  }

  function appendChatMessage(message) {
    const app = document.querySelector("[data-chat-app]");
    const messageList = app?.querySelector("[data-chat-message-list]");
    const currentUserId = Number(app?.dataset.currentUserId || 0);
    if (!messageList || !message || !message.id) return;
    if (messageList.querySelector(`[data-chat-message-id="${message.id}"]`)) return;

    const isMine = Number(message.senderUserId || 0) === currentUserId;
    const readState = isMine
      ? `<span class="chat-message-read-state" data-message-read-status>${message.readAtUtc ? `Leido ${escapeHtml(formatChatDateTime(message.readAtUtc))}` : "Enviado"}</span>`
      : "";

    messageList.insertAdjacentHTML("beforeend", `
      <article class="chat-message ${isMine ? "is-mine" : ""}" data-chat-message-id="${escapeAttribute(message.id)}">
        <div class="chat-message-bubble">
          <p>${escapeHtml(message.body || "")}</p>
        </div>
        <div class="chat-message-meta">
          <time datetime="${escapeAttribute(message.createdAtUtc || "")}">${escapeHtml(formatChatDateTime(message.createdAtUtc))}</time>
          ${readState}
        </div>
      </article>
    `);

    messageList.scrollTop = messageList.scrollHeight;
  }

  function applyReadReceiptUpdate(payload) {
    if (Number(payload?.conversationId || 0) !== activeChatConversationId) {
      return;
    }

    const readAt = formatChatDateTime(payload?.readAtUtc);
    const messageIds = Array.isArray(payload?.messageIds) ? payload.messageIds : [];
    messageIds.forEach(messageId => {
      const status = document.querySelector(`[data-chat-message-id="${messageId}"] [data-message-read-status]`);
      if (status) {
        status.textContent = `Leido ${readAt}`;
      }
    });
  }

  function formatChatDateTime(value) {
    const date = value ? new Date(value) : null;
    if (!date || Number.isNaN(date.getTime())) {
      return "";
    }

    return date.toLocaleString("es-AR", {
      day: "2-digit",
      month: "2-digit",
      hour: "2-digit",
      minute: "2-digit"
    }).replace(",", "");
  }

  function escapeHtml(value) {
    return String(value || "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll("\"", "&quot;")
      .replaceAll("'", "&#39;");
  }

  function escapeAttribute(value) {
    return escapeHtml(value);
  }
})();
