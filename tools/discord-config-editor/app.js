(() => {
  "use strict";

  const EVENTS = [
    { key: "player_warn_issued", title: "Варн игроку", group: "Варны игроков", route: "PlayerWebhook" },
    { key: "player_warn_removed", title: "Снятие варна", group: "Варны игроков", route: "PlayerWebhook" },
    { key: "moderator_warn_issued", title: "Варн модератору", group: "Варны модераторов", route: "ModeratorWebhook" },
    { key: "moderator_warn_removed", title: "Снятие варна", group: "Варны модераторов", route: "ModeratorWebhook" },
    { key: "ban", title: "Бан", group: "Наказания", route: "Punishments" },
    { key: "mute", title: "Мут", group: "Наказания", route: "Punishments" },
    { key: "gag", title: "Гаг", group: "Наказания", route: "Punishments" },
    { key: "silence", title: "Сайленс", group: "Наказания", route: "Punishments" },
    { key: "kick", title: "Кик", group: "Наказания", route: "Punishments" },
    { key: "unban", title: "Снятие бана", group: "Наказания", route: "Punishments" },
    { key: "uncomm", title: "Снятие мута/гага", group: "Наказания", route: "Punishments" },
    { key: "expired", title: "Истёк срок", group: "Наказания", route: "Punishments" },
    { key: "report", title: "Отчёт", group: "Система", route: "Reports" },
    { key: "anomaly", title: "Аномалия", group: "Система", route: "Anomalies" }
  ];
  const GROUPS = ["Варны игроков", "Варны модераторов", "Наказания", "Система"];
  const VARIABLES = [
    ["Игрок", "player", "Имя цели"], ["Игрок", "steamid", "SteamID64 цели"],
    ["Игрок", "playerurl", "Ссылка на Steam-профиль"], ["Игрок", "admin", "Выдал или снял"],
    ["Игрок", "adminsteamid", "SteamID64 администратора"], ["Игрок", "adminurl", "Профиль администратора"],
    ["Наказание", "reason", "Причина"], ["Наказание", "removereason", "Причина снятия"],
    ["Наказание", "duration", "Длительность"], ["Наказание", "durationseconds", "Длительность в секундах"],
    ["Наказание", "type", "Тип события"], ["Наказание", "warningid", "ID варна"],
    ["Наказание", "source", "Источник варна"], ["Наказание", "message", "Текст нарушения"],
    ["Наказание", "originalissuer", "Первоначально выдал"], ["Наказание", "test", "Тестовый варн"],
    ["Сервер", "servername", "Название сервера"], ["Сервер", "serverid", "ID сервера"],
    ["Сервер", "serverip", "IP:порт сервера"], ["Сервер", "online", "Игроков онлайн"],
    ["Время", "issuedat", "Дата выдачи"], ["Время", "expiresat", "Дата окончания"],
    ["Время", "removedat", "Дата снятия"], ["Время", "createdunix", "Unix для <t:...:F>"],
    ["Время", "issuediso", "ISO для timestamp"], ["Время", "now", "Текущее время"],
    ["Отчёт", "period", "Период"], ["Отчёт", "author", "Автор отчёта"],
    ["Отчёт", "count", "Всего событий"], ["Отчёт", "bans", "Банов"],
    ["Отчёт", "kicks", "Киков"], ["Отчёт", "comms", "Наказаний чата"],
    ["Отчёт", "warnings", "Варнов"], ["Отчёт", "removedwarnings", "Снятых варнов"],
    ["Отчёт", "threshold", "Порог аномалии"]
  ];
  const ALIASES = {
    target: "player", playername: "player", player_name: "player", steamid64: "steamid",
    playersteamid: "steamid", player_steamid: "steamid", player_url: "playerurl",
    issuer: "admin", adminname: "admin", admin_name: "admin", admin_steamid: "adminsteamid",
    admin_url: "adminurl", remove_reason: "removereason", duration_seconds: "durationseconds",
    durationminutes: "durationminutes", duration_minutes: "durationminutes", event: "type",
    warning_id: "warningid", original_issuer: "originalissuer", ismoderatorwarning: "ismoderatorwarning",
    server: "servername", server_name: "servername", server_id: "serverid", server_ip: "serverip",
    issued_at: "issuedat", createdat: "issuedat", expires_at: "expiresat", removed_at: "removedat",
    created_unix: "createdunix", expires_unix: "expiresunix", removed_unix: "removedunix",
    issued_iso: "issuediso", expires_iso: "expiresiso", removed_iso: "removediso",
    nowunix: "nowunix", nowiso: "nowiso", emoji: "emoji", unbans: "unbans", uncomms: "uncomms"
  };
  const DEFAULT_SAMPLES = {
    player: "Player One", steamid: "76561198000000000", admin: "Moderator", adminsteamid: "76561198000000001",
    reason: "Нарушение правил чата", removereason: "Апелляция одобрена", warningid: "42",
    source: "moderator", message: "Пример сообщения в чате", servername: "IksAdmin CS2", serverid: "1",
    serverip: "127.0.0.1:27015", online: "24", durationseconds: "3600", period: "day",
    author: "Moderator", count: "18", bans: "3", kicks: "2", comms: "5", warnings: "6",
    removedwarnings: "2", threshold: "10", unbans: "1", uncomms: "1", test: "false"
  };
  const INITIAL_MESSAGE = {
    username: "IksAdmin Logs",
    content: "",
    embeds: [{ title: "Варн #{warningid}", description: "**{player}** получил варн. Причина: {reason}", color: 15105570,
      fields: [{ name: "SteamID64", value: "{steamid}", inline: true }, { name: "Сервер", value: "{servername}", inline: true }] }],
    components: [{ type: 1, components: [{ type: 2, style: 5, label: "Профиль Steam", url: "{playerurl}" }] }]
  };
  const state = {
    main: { Version: 1, Webhooks: { Punishments: "", Reports: "", Anomalies: "", Errors: "" }, Messages: { player_warn_issued: INITIAL_MESSAGE } },
    warnings: { Enabled: true, PlayerWebhook: "", ModeratorWebhook: "", Issued: true, Removed: true, Automatic: true, Test: true },
    samples: { ...DEFAULT_SAMPLES }, selected: "player_warn_issued", drafts: {}, tab: "visual", lastInput: null
  };
  const $ = id => document.getElementById(id);
  const clone = value => structuredClone(value);
  const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[char]);
  const safeUrl = value => { try { const url = new URL(value); return url.protocol === "https:" || url.protocol === "http:" ? url.href : ""; } catch { return ""; } };
  const hexColor = value => `#${(Number(value) || 0).toString(16).padStart(6, "0").slice(-6)}`;
  const currentEvent = () => EVENTS.find(event => event.key === state.selected);
  const configured = () => Object.hasOwn(state.main.Messages, state.selected);
  const defaultMessage = () => ({ username: "IksAdmin Logs", content: "", embeds: [{ title: currentEvent().title, description: "**{player}**: {reason}", color: 5814783, fields: [] }] });
  const currentMessage = () => state.main.Messages[state.selected] ?? (state.drafts[state.selected] ||= defaultMessage());
  const plainObject = value => value !== null && typeof value === "object" && !Array.isArray(value);
  function checkMessageShape(message) {
    if (!plainObject(message)) throw new Error("Ожидается объект сообщения Discord.");
    for (const key of ["username", "avatar_url", "content"]) {
      if (message[key] !== undefined && typeof message[key] !== "string") throw new Error(`${key} должен быть строкой.`);
    }
    for (const key of ["embeds", "components"]) {
      if (message[key] !== undefined && !Array.isArray(message[key])) throw new Error(`${key} должен быть массивом.`);
    }
    for (const embed of message.embeds || []) {
      if (!plainObject(embed)) throw new Error("Каждый embed должен быть объектом.");
      for (const key of ["author", "footer", "image", "thumbnail"]) {
        if (embed[key] !== undefined && !plainObject(embed[key])) throw new Error(`${key} должен быть объектом.`);
      }
      for (const key of ["title", "description"]) {
        if (embed[key] !== undefined && typeof embed[key] !== "string") throw new Error(`${key} должен быть строкой.`);
      }
      if (embed.fields !== undefined && !Array.isArray(embed.fields)) throw new Error("fields должен быть массивом.");
      if ((embed.fields || []).some(field => !plainObject(field) ||
          (field.name !== undefined && typeof field.name !== "string") ||
          (field.value !== undefined && typeof field.value !== "string"))) throw new Error("Поля embed должны содержать строковые name и value.");
    }
    for (const row of message.components || []) {
      if (!plainObject(row) || !Array.isArray(row.components) || row.components.some(button => !plainObject(button)))
        throw new Error("components должен содержать строки с массивами кнопок.");
    }
    return message;
  }
  function checkMainConfig(data) {
    if (!plainObject(data) || (data.Messages !== undefined && !plainObject(data.Messages)) ||
        (data.Webhooks !== undefined && !plainObject(data.Webhooks))) throw new Error("Неверная структура основного конфига.");
    for (const message of Object.values(data.Messages || {})) checkMessageShape(message);
    return data;
  }
  function checkWarningsConfig(data) {
    if (!plainObject(data) || ["PlayerWebhook", "ModeratorWebhook"].some(key =>
      data[key] !== undefined && typeof data[key] !== "string")) throw new Error("Неверная структура warnings.json.");
    return data;
  }
  const useMessage = () => { if (!configured()) state.main.Messages[state.selected] = state.drafts[state.selected] || defaultMessage(); markChanged(); return state.main.Messages[state.selected]; };
  let toastTimer;

  function markChanged() { $("save-state").textContent = "Изменено"; renderEventList(); }
  function toast(text) { const el = $("toast"); el.textContent = text; el.classList.remove("hidden"); clearTimeout(toastTimer); toastTimer = setTimeout(() => el.classList.add("hidden"), 3600); }
  function download(name, data) {
    const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2) + "\n"], { type: "application/json;charset=utf-8" }));
    const link = document.createElement("a"); link.href = url; link.download = name; link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    $("save-state").textContent = "Скачано";
  }
  async function copyText(value) {
    try { await navigator.clipboard.writeText(value); }
    catch {
      const area = document.createElement("textarea"); area.value = value; area.style.position = "fixed"; area.style.opacity = "0";
      document.body.append(area); area.select(); document.execCommand("copy"); area.remove();
    }
    toast("JSON скопирован");
  }
  function renderEventList() {
    const count = EVENTS.filter(event => Object.hasOwn(state.main.Messages, event.key)).length;
    $("configured-count").textContent = `${count} / ${EVENTS.length}`;
    $("event-list").innerHTML = GROUPS.map(group => `<div class="event-group">${escapeHtml(group)}</div>` + EVENTS.filter(event => event.group === group).map(event =>
      `<button class="event-button ${state.selected === event.key ? "active" : ""}" data-event="${event.key}" type="button"><span class="event-dot ${Object.hasOwn(state.main.Messages, event.key) ? "configured" : ""}"></span><span class="event-title">${escapeHtml(event.title)}</span></button>`).join("")).join("");
  }
  function routeState() {
    const event = currentEvent();
    const url = event.route.endsWith("Webhook") ? state.warnings[event.route] : state.main.Webhooks?.[event.route];
    const label = { PlayerWebhook: "Варны игроков", ModeratorWebhook: "Варны модераторов", Punishments: "Наказания", Reports: "Отчёты", Anomalies: "Аномалии" }[event.route];
    return { label, ready: Boolean(url?.trim()), disabled: event.route.endsWith("Webhook") && state.warnings.Enabled === false };
  }
  function renderHeading() {
    const event = currentEvent(); const route = routeState();
    $("event-category").textContent = event.group;
    $("event-title").textContent = event.title;
    $("route-label").textContent = `${configured() ? "Свой шаблон" : "Черновик нового шаблона"} · ${route.label} · ${route.disabled ? "отключено" : route.ready ? "webhook задан" : "webhook не задан"}`;
    $("preview-destination").textContent = `${route.label} · ${route.disabled ? "отправка отключена" : route.ready ? "webhook настроен" : "webhook не указан"}`;
  }
  function setNested(object, path, value) {
    const parts = path.split("."); let parent = object;
    for (const part of parts.slice(0, -1)) parent = parent[part] ||= {};
    if (value === "") delete parent[parts.at(-1)]; else parent[parts.at(-1)] = value;
    for (let i = parts.length - 2; i >= 0; i--) {
      const container = parts.slice(0, i).reduce((node, part) => node[part], object);
      if (Object.keys(container[parts[i]]).length === 0) delete container[parts[i]];
    }
  }
  function renderVisual() {
    const message = currentMessage();
    $("message-username").value = message.username || "";
    $("message-avatar").value = message.avatar_url || "";
    $("message-content").value = message.content || "";
    $("content-limit").textContent = `${(message.content || "").length} / 2000`;
    renderEmbedsEditor(); renderButtonsEditor();
  }
  function renderEmbedsEditor() {
    const embeds = currentMessage().embeds || [];
    $("embeds-editor").innerHTML = embeds.length ? embeds.map((embed, index) => `
      <div class="embed-editor" data-embed="${index}">
        <div class="embed-header"><strong>Embed ${index + 1}</strong><div class="inline-actions"><button class="icon-action" data-move-embed="-1" title="Поднять" aria-label="Поднять embed" type="button">↑</button><button class="icon-action" data-move-embed="1" title="Опустить" aria-label="Опустить embed" type="button">↓</button><button class="icon-action danger" data-remove-embed title="Удалить" aria-label="Удалить embed" type="button">×</button></div></div>
        <div class="embed-grid"><label>Заголовок<input data-embed-key="title" maxlength="256" value="${escapeHtml(embed.title)}" placeholder="Заголовок"></label>
        <label>Описание<textarea data-embed-key="description" rows="4" maxlength="4096" placeholder="Текст embed">${escapeHtml(embed.description)}</textarea></label>
        <div class="color-line"><label>Цвет<input data-embed-key="color" data-color-number type="text" value="${Number(embed.color) || 0}" inputmode="numeric"></label><input data-embed-key="color" data-color-picker type="color" value="${hexColor(embed.color)}" aria-label="Выбрать цвет"></div>
        <div class="two-col"><label>Автор<input data-embed-key="author.name" value="${escapeHtml(embed.author?.name)}" placeholder="Имя автора"></label><label>Иконка автора<input data-embed-key="author.icon_url" value="${escapeHtml(embed.author?.icon_url)}" placeholder="https://..."></label></div>
        <div class="two-col"><label>Изображение<input data-embed-key="image.url" value="${escapeHtml(embed.image?.url)}" placeholder="https://..."></label><label>Миниатюра<input data-embed-key="thumbnail.url" value="${escapeHtml(embed.thumbnail?.url)}" placeholder="https://..."></label></div>
        <label>Подпись<input data-embed-key="footer.text" value="${escapeHtml(embed.footer?.text)}" placeholder="Текст под embed"></label></div>
        <div class="field-list"><div class="field-header"><strong>Поля (${embed.fields?.length || 0})</strong><button class="button small" data-add-field type="button">Добавить поле</button></div>
        ${(embed.fields || []).map((field, fieldIndex) => `<div class="field-row" data-field="${fieldIndex}"><div class="field-header"><span class="muted">Поле ${fieldIndex + 1}</span><button class="icon-action danger" data-remove-field title="Удалить поле" aria-label="Удалить поле" type="button">×</button></div><div class="two-col"><label>Название<input data-field-key="name" maxlength="256" value="${escapeHtml(field.name)}"></label><label>Значение<input data-field-key="value" maxlength="1024" value="${escapeHtml(field.value)}"></label></div><label class="check-line"><input data-field-key="inline" type="checkbox" ${field.inline ? "checked" : ""}> В одну строку</label></div>`).join("")}</div>
      </div>`).join("") : `<div class="empty-editor">Нет embeds. Текст сообщения можно отправлять отдельно.</div>`;
  }
  function allButtons() { return (currentMessage().components || []).flatMap(row => row?.components || []); }
  function unsupportedComponents() { return (currentMessage().components || []).some(row => row?.type !== 1 || (row.components || []).some(button => button.type !== 2 || button.style !== 5)); }
  function setButtons(buttons) {
    const message = useMessage();
    if (!buttons.length) { delete message.components; return; }
    message.components = [];
    for (let index = 0; index < buttons.length; index += 5)
      message.components.push({ type: 1, components: buttons.slice(index, index + 5) });
  }
  function renderButtonsEditor() {
    const buttons = allButtons();
    if (unsupportedComponents()) {
      $("buttons-editor").innerHTML = `<div class="empty-editor">В JSON есть интерактивные компоненты. Плагин поддерживает только ссылочные кнопки (style 5). Измените их на вкладке JSON.</div>`;
      return;
    }
    $("buttons-editor").innerHTML = buttons.length ? buttons.map((button, index) => `
      <div class="button-row" data-button="${index}"><div class="button-header"><strong>Ссылка ${index + 1}</strong><div class="inline-actions"><button class="icon-action" data-move-button="-1" title="Поднять" aria-label="Поднять кнопку" type="button">↑</button><button class="icon-action" data-move-button="1" title="Опустить" aria-label="Опустить кнопку" type="button">↓</button><button class="icon-action danger" data-remove-button title="Удалить" aria-label="Удалить кнопку" type="button">×</button></div></div><div class="two-col"><label>Текст<input data-button-key="label" maxlength="80" value="${escapeHtml(button.label)}"></label><label>HTTPS-ссылка<input data-button-key="url" value="${escapeHtml(button.url)}" placeholder="https://..."></label></div></div>`).join("") : `<div class="empty-editor">Нет кнопок. Для webhook доступны HTTPS-ссылки.</div>`;
  }
  function renderVariables() {
    const search = $("variable-search").value.trim().toLowerCase();
    $("variable-list").innerHTML = [...new Set(VARIABLES.map(row => row[0]))].map(group => {
      const rows = VARIABLES.filter(row => row[0] === group && (!search || `${row[1]} ${row[2]}`.toLowerCase().includes(search)));
      return rows.length ? `<div class="variable-group"><h3>${group}</h3>${rows.map(row => `<div class="variable-row"><button class="button small" data-variable="${row[1]}" type="button"><code>{${row[1]}}</code></button><span>${escapeHtml(row[2])}</span></div>`).join("")}</div>` : "";
    }).join("") || `<div class="empty-editor">Ничего не найдено.</div>`;
  }
  function sampleValues() {
    const now = new Date(); const expires = new Date(now.getTime() + Number(state.samples.durationseconds || 0) * 1000);
    const steam = state.samples.steamid || ""; const adminSteam = state.samples.adminsteamid || "";
    return {
      ...state.samples, playerurl: /^\d{17}$/.test(steam) ? `https://steamcommunity.com/profiles/${steam}` : "",
      adminurl: /^\d{17}$/.test(adminSteam) ? `https://steamcommunity.com/profiles/${adminSteam}` : "",
      duration: `${Math.round(Number(state.samples.durationseconds || 0) / 60)} мин.`,
      durationminutes: String(Math.floor(Number(state.samples.durationseconds || 0) / 60)),
      type: state.selected.replace(/^(player|moderator)_/, ""), ismoderatorwarning: String(state.selected.startsWith("moderator_")),
      issuedat: now.toLocaleString("ru-RU"), expiresat: expires.toLocaleString("ru-RU"), removedat: now.toLocaleString("ru-RU"),
      createdunix: String(Math.floor(now.getTime() / 1000)), expiresunix: String(Math.floor(expires.getTime() / 1000)),
      removedunix: String(Math.floor(now.getTime() / 1000)), issuediso: now.toISOString(), expiresiso: expires.toISOString(),
      removediso: now.toISOString(), now: now.toLocaleString("ru-RU"), nowunix: String(Math.floor(now.getTime() / 1000)),
      nowiso: now.toISOString(), emoji: "📋"
    };
  }
  const placeholderPattern = /\{([a-z][a-z0-9_]*)\}/gi;
  function expand(text, values) {
    return String(text ?? "").replace(placeholderPattern, (match, key) => {
      const canonical = ALIASES[key.toLowerCase()] || key.toLowerCase();
      return Object.hasOwn(values, canonical) ? String(values[canonical]) : match;
    });
  }
  function renderMarkdown(text) {
    let html = escapeHtml(text);
    html = html.replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g, (_match, label, url) => `<a href="${url}" target="_blank" rel="noopener noreferrer">${label}</a>`);
    html = html.replace(/`([^`]+)`/g, "<code>$1</code>");
    html = html.replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>");
    html = html.replace(/~~([^~]+)~~/g, "<s>$1</s>");
    html = html.replace(/(?<!\*)\*([^*\n]+)\*(?!\*)/g, "<em>$1</em>");
    return html;
  }
  function validate(message, values) {
    const errors = [];
    if (message.attachments?.length) errors.push("Загруженные файлы Discohook не отправляются через JSON webhook.");
    if (!(message.content || "").trim() && !(message.embeds || []).length) errors.push("Нужен текст сообщения или хотя бы один embed.");
    if ((message.content || "").length > 2000) errors.push("Текст длиннее 2000 символов.");
    if ((message.username || "").length > 80) errors.push("Имя webhook длиннее 80 символов.");
    if ((message.embeds || []).length > 10) errors.push("Discord допускает не более 10 embeds.");
    let embedLength = 0;
    for (const embed of message.embeds || []) {
      const title = expand(embed.title, values), description = expand(embed.description, values);
      const footer = expand(embed.footer?.text, values), author = expand(embed.author?.name, values);
      if (title.length > 256 || description.length > 4096 || footer.length > 2048 || author.length > 256) errors.push("Превышен лимит заголовка, описания, автора или подписи embed.");
      embedLength += title.length + description.length + footer.length + author.length;
      if ((embed.fields || []).length > 25) errors.push("В одном embed допускается не более 25 полей.");
      for (const field of embed.fields || []) {
        const name = expand(field.name, values), value = expand(field.value, values);
        if (name.length < 1 || name.length > 256 || value.length < 1 || value.length > 1024) errors.push("Поле embed должно иметь название (1–256) и значение (1–1024). ");
        embedLength += name.length + value.length;
      }
    }
    if (embedLength > 6000) errors.push("Суммарный текст embeds длиннее 6000 символов.");
    if ((message.components || []).length > 5) errors.push("Discord допускает не более пяти строк кнопок.");
    for (const row of message.components || []) {
      if (row.type !== 1 || !Array.isArray(row.components) || row.components.length > 5) { errors.push("Неверный формат строки кнопок."); continue; }
      for (const button of row.components) {
        if (button.type !== 2 || button.style !== 5) errors.push("Плагин поддерживает только ссылочные кнопки style 5.");
        if (!button.label || button.label.length > 80 || !safeUrl(expand(button.url, values))?.startsWith("https:")) errors.push("Кнопке нужны текст до 80 символов и HTTPS-ссылка.");
      }
    }
    const unknown = new Set();
    JSON.stringify(message).replace(placeholderPattern, (match, key) => {
      if (!Object.hasOwn(values, ALIASES[key.toLowerCase()] || key.toLowerCase())) unknown.add(match);
      return match;
    });
    if (unknown.size) errors.push(`Неизвестные заполнители: ${[...unknown].join(", ")}.`);
    return [...new Set(errors)];
  }
  function renderPreview() {
    const message = currentMessage(); const values = sampleValues(); const errors = validate(message, values);
    $("validation").classList.toggle("hidden", !errors.length);
    $("validation").textContent = errors.join("\n");
    $("preview-username").textContent = expand(message.username || "IksAdmin Logs", values);
    const avatar = safeUrl(expand(message.avatar_url, values));
    $("preview-avatar").innerHTML = avatar ? `<img src="${escapeHtml(avatar)}" alt="">` : "IK";
    $("preview-content").innerHTML = renderMarkdown(expand(message.content || "", values));
    $("preview-embeds").replaceChildren();
    for (const embed of message.embeds || []) {
      const section = document.createElement("div"); section.className = "discord-embed"; section.style.borderLeftColor = hexColor(embed.color);
      if (embed.thumbnail?.url) {
        const url = safeUrl(expand(embed.thumbnail.url, values));
        if (url) section.innerHTML += `<img class="discord-thumb" src="${escapeHtml(url)}" alt="">`;
      }
      if (embed.author?.name) {
        const author = document.createElement("div"); author.className = "discord-embed-author";
        const icon = safeUrl(expand(embed.author.icon_url, values));
        if (icon) author.innerHTML = `<img src="${escapeHtml(icon)}" alt="">`;
        const name = document.createElement("span"); name.textContent = expand(embed.author.name, values); author.append(name); section.append(author);
      }
      if (embed.title) { const title = document.createElement("div"); title.className = "discord-embed-title"; title.innerHTML = renderMarkdown(expand(embed.title, values)); section.append(title); }
      if (embed.description) { const description = document.createElement("div"); description.className = "discord-description"; description.innerHTML = renderMarkdown(expand(embed.description, values)); section.append(description); }
      if (embed.fields?.length) {
        const fields = document.createElement("div"); fields.className = "discord-fields";
        for (const field of embed.fields) {
          const item = document.createElement("div"); item.className = `discord-field${field.inline ? " inline" : ""}`;
          const name = document.createElement("div"); name.className = "discord-field-name"; name.innerHTML = renderMarkdown(expand(field.name, values));
          const value = document.createElement("div"); value.className = "discord-field-value"; value.innerHTML = renderMarkdown(expand(field.value, values));
          item.append(name, value); fields.append(item);
        }
        section.append(fields);
      }
      const image = safeUrl(expand(embed.image?.url, values));
      if (image) { const img = document.createElement("img"); img.className = "discord-media"; img.src = image; img.alt = "Изображение embed"; section.append(img); }
      if (embed.footer?.text) { const footer = document.createElement("div"); footer.className = "discord-footer"; footer.textContent = expand(embed.footer.text, values); section.append(footer); }
      $("preview-embeds").append(section);
    }
    $("preview-buttons").replaceChildren();
    for (const row of message.components || []) {
      const line = document.createElement("div"); line.className = "discord-buttons";
      for (const button of row.components || []) {
        const link = document.createElement("a"); link.className = "discord-link"; link.textContent = `${expand(button.label, values)} ↗`;
        const url = safeUrl(expand(button.url, values)); link.href = url || "#"; link.target = "_blank"; link.rel = "noopener noreferrer";
        if (!url) link.addEventListener("click", event => event.preventDefault()); line.append(link);
      }
      $("preview-buttons").append(line);
    }
    $("preview-size").textContent = `${(message.content || "").length + (message.embeds || []).reduce((sum, item) => sum + (item.title || "").length + (item.description || "").length, 0)} символов · ${errors.length ? `${errors.length} замечаний` : "готово"}`;
  }
  function renderAll() { renderEventList(); renderHeading(); renderVisual(); renderPreview(); renderVariables(); }
  function applyRaw() {
    try {
      const parsed = JSON.parse($("raw-json").value);
      let message = parsed;
      if (parsed.Messages) message = parsed.Messages[state.selected];
      else if (parsed[state.selected]) message = parsed[state.selected];
      state.main.Messages[state.selected] = checkMessageShape(message);
      delete state.drafts[state.selected]; markChanged(); renderHeading(); renderVisual(); renderPreview();
      toast("JSON применён"); return true;
    } catch (error) { toast(`Ошибка JSON: ${error.message}`); return false; }
  }
  function setTab(tab) {
    if (state.tab === "json" && tab !== "json" && $("raw-json").dataset.dirty === "true") {
      if (!applyRaw()) return;
      $("raw-json").dataset.dirty = "false";
    }
    state.tab = tab;
    for (const name of ["visual", "json", "variables"]) {
      $(`${name}-view`).classList.toggle("hidden", name !== tab);
      $(`tab-${name}`).classList.toggle("active", name === tab);
      $(`tab-${name}`).setAttribute("aria-selected", String(name === tab));
    }
    if (tab === "json" && $("raw-json").dataset.dirty !== "true") refreshRaw();
  }
  function refreshRaw() { $("raw-json").value = JSON.stringify(currentMessage(), null, 2); $("raw-json").dataset.dirty = "false"; }
  function savePendingRaw() {
    if (state.tab !== "json" || $("raw-json").dataset.dirty !== "true") return true;
    if (!applyRaw()) return false;
    $("raw-json").dataset.dirty = "false";
    return true;
  }
  function selectEvent(key) {
    if (!EVENTS.some(event => event.key === key)) return;
    if (!savePendingRaw()) return;
    state.selected = key; renderAll(); if (state.tab === "json") refreshRaw();
  }
  function settingInput(label, path, value, secret = false) {
    return `<label>${escapeHtml(label)}<div class="webhook-line"><input data-setting="${path}" type="${secret ? "password" : "text"}" value="${escapeHtml(value)}" placeholder="${secret ? "https://discord.com/api/webhooks/..." : ""}">${secret ? '<button class="button small subtle" data-reveal type="button">Показать</button>' : ""}</div></label>`;
  }
  function settingCheckbox(label, path, value) { return `<label class="check-line"><input type="checkbox" data-setting="${path}" ${value ? "checked" : ""}> ${escapeHtml(label)}</label>`; }
  function renderSettings() {
    const webhooks = state.main.Webhooks ||= {};
    $("webhook-settings").innerHTML = `<div class="settings-group"><h3>Каналы Discord</h3><div class="stack">
      ${settingInput("Наказания", "main.Webhooks.Punishments", webhooks.Punishments, true)}
      ${settingInput("Варны игроков", "warnings.PlayerWebhook", state.warnings.PlayerWebhook, true)}
      ${settingInput("Варны модераторов", "warnings.ModeratorWebhook", state.warnings.ModeratorWebhook, true)}
      ${settingInput("Отчёты", "main.Webhooks.Reports", webhooks.Reports, true)}
      ${settingInput("Аномалии", "main.Webhooks.Anomalies", webhooks.Anomalies, true)}
      ${settingInput("Ошибки", "main.Webhooks.Errors", webhooks.Errors, true)}
    </div></div>`;
    const reports = state.main.Reports ||= {};
    $("warning-settings").innerHTML = `<div class="settings-group"><h3>Варны</h3><div class="stack">
      ${settingCheckbox("Отправка включена", "warnings.Enabled", state.warnings.Enabled !== false)}
      ${settingCheckbox("Выдача", "warnings.Issued", state.warnings.Issued !== false)}
      ${settingCheckbox("Снятие", "warnings.Removed", state.warnings.Removed !== false)}
      ${settingCheckbox("Автоматические варны", "warnings.Automatic", state.warnings.Automatic !== false)}
      ${settingCheckbox("Тестовые варны", "warnings.Test", state.warnings.Test !== false)}
    </div></div><div class="settings-divider"></div><div class="settings-group"><h3>Отчёты и аномалии</h3><div class="stack">
      ${settingCheckbox("Отчёты включены", "main.Reports.Enabled", reports.Enabled !== false)}
      ${settingCheckbox("Плановые отчёты", "main.Reports.SendScheduledReports", reports.SendScheduledReports === true)}
      ${settingInput("Интервал, минуты", "main.Reports.IntervalMinutes", reports.IntervalMinutes ?? 60)}
      ${settingInput("Порог аномалии за час", "main.Reports.AnomalyThresholdPerHour", reports.AnomalyThresholdPerHour ?? 10)}
    </div></div>`;
  }
  function renderSamples() {
    const names = { player: "Игрок", steamid: "SteamID64", admin: "Администратор", adminsteamid: "SteamID64 админа", reason: "Причина", removereason: "Причина снятия", warningid: "ID варна", source: "Источник", message: "Сообщение", servername: "Сервер", serverid: "ID сервера", serverip: "IP:порт", online: "Онлайн", durationseconds: "Длительность, сек", period: "Период", author: "Автор отчёта", count: "Событий", bans: "Банов", kicks: "Киков", comms: "Ограничений чата", warnings: "Варнов", removedwarnings: "Снятых варнов", threshold: "Порог", unbans: "Снятых банов", uncomms: "Снятых ограничений", test: "Тест" };
    $("sample-fields").innerHTML = Object.entries(names).map(([key, name]) => `<label>${name}<input data-sample="${key}" value="${escapeHtml(state.samples[key])}"></label>`).join("");
  }
  function updateSetting(path, value) {
    const parts = path.split("."); const root = parts.shift(); let object = state[root];
    for (const part of parts.slice(0, -1)) object = object[part] ||= {};
    object[parts.at(-1)] = value;
    markChanged(); renderHeading(); renderPreview();
  }
  async function readJson(file) { return JSON.parse(await file.text()); }
  function attachHandlers() {
    $("event-list").addEventListener("click", event => { const button = event.target.closest("[data-event]"); if (button) selectEvent(button.dataset.event); });
    for (const tab of ["visual", "json", "variables"]) $(`tab-${tab}`).addEventListener("click", () => setTab(tab));
    $("visual-view").addEventListener("focusin", event => { if (event.target.matches("input,textarea")) state.lastInput = event.target; });
    $("visual-view").addEventListener("input", event => {
      const input = event.target;
      if (input.dataset.path) {
        const message = useMessage(); setNested(message, input.dataset.path, input.value);
        $("content-limit").textContent = `${(message.content || "").length} / 2000`;
      } else if (input.dataset.embedKey) {
        const index = Number(input.closest("[data-embed]").dataset.embed); const embed = useMessage().embeds[index];
        const value = input.dataset.embedKey === "color" ? (input.dataset.colorPicker !== undefined ? parseInt(input.value.slice(1), 16) : Number(input.value) || 0) : input.value;
        setNested(embed, input.dataset.embedKey, value);
        if (input.dataset.colorPicker !== undefined) input.parentElement.querySelector("[data-color-number]").value = String(value);
        if (input.dataset.colorNumber !== undefined) input.parentElement.parentElement.querySelector("[data-color-picker]").value = hexColor(value);
      } else if (input.dataset.fieldKey) {
        const embedIndex = Number(input.closest("[data-embed]").dataset.embed), fieldIndex = Number(input.closest("[data-field]").dataset.field);
        useMessage().embeds[embedIndex].fields[fieldIndex][input.dataset.fieldKey] = input.type === "checkbox" ? input.checked : input.value;
      } else if (input.dataset.buttonKey) {
        const buttons = clone(allButtons()); buttons[Number(input.closest("[data-button]").dataset.button)][input.dataset.buttonKey] = input.value;
        setButtons(buttons);
      }
      renderHeading(); renderPreview();
    });
    $("visual-view").addEventListener("change", event => { if (event.target.type === "checkbox") event.target.dispatchEvent(new Event("input", { bubbles: true })); });
    $("embeds-editor").addEventListener("click", event => {
      const button = event.target.closest("button"); if (!button) return;
      const embedIndex = Number(button.closest("[data-embed]").dataset.embed), message = useMessage(), embeds = message.embeds;
      if (button.hasAttribute("data-remove-embed")) embeds.splice(embedIndex, 1);
      else if (button.hasAttribute("data-move-embed")) { const other = embedIndex + Number(button.dataset.moveEmbed); if (other < 0 || other >= embeds.length) return; [embeds[embedIndex], embeds[other]] = [embeds[other], embeds[embedIndex]]; }
      else if (button.hasAttribute("data-add-field")) { (embeds[embedIndex].fields ||= []).push({ name: "Название", value: "Значение", inline: false }); }
      else if (button.hasAttribute("data-remove-field")) embeds[embedIndex].fields.splice(Number(button.closest("[data-field]").dataset.field), 1);
      else return;
      renderEmbedsEditor(); renderPreview(); renderHeading();
    });
    $("buttons-editor").addEventListener("click", event => {
      const button = event.target.closest("button"); if (!button || unsupportedComponents()) return;
      const index = Number(button.closest("[data-button]").dataset.button), buttons = clone(allButtons());
      if (button.hasAttribute("data-remove-button")) buttons.splice(index, 1);
      else if (button.hasAttribute("data-move-button")) { const other = index + Number(button.dataset.moveButton); if (other < 0 || other >= buttons.length) return; [buttons[index], buttons[other]] = [buttons[other], buttons[index]]; }
      else return;
      setButtons(buttons); renderButtonsEditor(); renderPreview(); renderHeading();
    });
    $("add-embed").addEventListener("click", () => { const message = useMessage(); if ((message.embeds ||= []).length >= 10) return toast("Лимит: 10 embeds"); message.embeds.push({ title: "Новый embed", description: "", color: 5814783, fields: [] }); renderEmbedsEditor(); renderPreview(); renderHeading(); });
    $("add-button").addEventListener("click", () => { if (unsupportedComponents()) return toast("Сначала исправьте компоненты в JSON"); const buttons = clone(allButtons()); if (buttons.length >= 25) return toast("Лимит: 25 кнопок"); buttons.push({ type: 2, style: 5, label: "Ссылка", url: "https://example.com" }); setButtons(buttons); renderButtonsEditor(); renderPreview(); renderHeading(); });
    $("variable-search").addEventListener("input", renderVariables);
    $("variable-list").addEventListener("click", async event => {
      const button = event.target.closest("[data-variable]"); if (!button) return;
      const token = `{${button.dataset.variable}}`, input = state.lastInput;
      if (input?.isConnected && input.matches("input:not([type=color]),textarea") && !input.closest("dialog")) {
        const start = input.selectionStart ?? input.value.length, end = input.selectionEnd ?? start;
        input.setRangeText(token, start, end, "end"); input.dispatchEvent(new Event("input", { bubbles: true })); input.focus();
        toast(`${token} вставлен`);
      } else await copyText(token);
    });
    $("raw-json").addEventListener("input", () => { $("raw-json").dataset.dirty = "true"; });
    $("apply-json").addEventListener("click", () => { if (applyRaw()) $("raw-json").dataset.dirty = "false"; });
    $("paste-discohook").addEventListener("click", async () => { try { $("raw-json").value = await navigator.clipboard.readText(); $("raw-json").dataset.dirty = "true"; toast("JSON вставлен. Нажмите «Применить JSON»."); } catch { toast("Нет доступа к буферу. Вставьте JSON вручную."); } });
    $("import-message-file").addEventListener("click", () => $("message-file").click());
    $("message-file").addEventListener("change", async event => { if (!event.target.files[0]) return; try { const data = await readJson(event.target.files[0]); setTab("json"); $("raw-json").value = JSON.stringify(data, null, 2); $("raw-json").dataset.dirty = "true"; toast("Файл загружен. Примените JSON."); } catch (error) { toast(`Ошибка файла: ${error.message}`); } event.target.value = ""; });
    $("clear-message").addEventListener("click", () => { if (!confirm("Удалить свой шаблон этого события? Стандартный embed плагина останется доступным.")) return; delete state.main.Messages[state.selected]; delete state.drafts[state.selected]; $("raw-json").dataset.dirty = "false"; markChanged(); renderAll(); if (state.tab === "json") refreshRaw(); });
    $("copy-message").addEventListener("click", () => { if (savePendingRaw()) copyText(JSON.stringify(currentMessage(), null, 2)); });
    $("download-message").addEventListener("click", () => { if (savePendingRaw()) download(`${state.selected}.json`, currentMessage()); });
    $("import-configs").addEventListener("click", () => $("import-dialog").showModal());
    $("open-settings").addEventListener("click", () => { renderSettings(); $("settings-dialog").showModal(); });
    $("export-configs").addEventListener("click", () => $("export-dialog").showModal());
    $("export-project").addEventListener("click", () => { if (savePendingRaw()) download("iksadmin-discord-project.json", { format: "iksadmin-discord-studio-v1", main: state.main, warnings: state.warnings, samples: state.samples }); });
    $("download-main-config").addEventListener("click", () => { if (savePendingRaw()) download("IksAdmin_DiscordPunishments.json", state.main); });
    $("download-warnings-config").addEventListener("click", () => download("warnings.json", state.warnings));
    document.querySelectorAll("[data-close-dialog]").forEach(button => button.addEventListener("click", () => button.closest("dialog").close()));
    for (const dialog of document.querySelectorAll("dialog")) dialog.addEventListener("click", event => { if (event.target === dialog) dialog.close(); });
    $("settings-dialog").addEventListener("input", event => {
      const input = event.target; if (!input.dataset.setting) return;
      const numeric = input.dataset.setting.endsWith("IntervalMinutes") || input.dataset.setting.endsWith("AnomalyThresholdPerHour");
      updateSetting(input.dataset.setting, input.type === "checkbox" ? input.checked : numeric ? Number(input.value) || 0 : input.value);
    });
    $("settings-dialog").addEventListener("change", event => { if (event.target.type === "checkbox") event.target.dispatchEvent(new Event("input", { bubbles: true })); });
    $("settings-dialog").addEventListener("click", event => { if (!event.target.hasAttribute("data-reveal")) return; const input = event.target.parentElement.querySelector("input"); input.type = input.type === "password" ? "text" : "password"; event.target.textContent = input.type === "password" ? "Показать" : "Скрыть"; });
    $("main-file").addEventListener("change", async event => { if (!event.target.files[0]) return; try { const data = checkMainConfig(await readJson(event.target.files[0])); state.main = data; state.main.Messages ||= {}; state.main.Webhooks ||= {}; state.drafts = {}; $("raw-json").dataset.dirty = "false"; markChanged(); renderAll(); if (state.tab === "json") refreshRaw(); toast("Основной конфиг загружен"); } catch (error) { toast(`Ошибка: ${error.message}`); } event.target.value = ""; });
    $("warnings-file").addEventListener("change", async event => { if (!event.target.files[0]) return; try { state.warnings = checkWarningsConfig(await readJson(event.target.files[0])); markChanged(); renderHeading(); toast("Конфиг варнов загружен"); } catch (error) { toast(`Ошибка: ${error.message}`); } event.target.value = ""; });
    $("project-file").addEventListener("change", async event => { if (!event.target.files[0]) return; try { const data = await readJson(event.target.files[0]); if (data.format !== "iksadmin-discord-studio-v1") throw new Error("Неверный формат проекта"); checkMainConfig(data.main); checkWarningsConfig(data.warnings); state.main = data.main; state.main.Messages ||= {}; state.main.Webhooks ||= {}; state.warnings = data.warnings; state.samples = { ...DEFAULT_SAMPLES, ...(plainObject(data.samples) ? data.samples : {}) }; state.drafts = {}; $("raw-json").dataset.dirty = "false"; renderAll(); if (state.tab === "json") refreshRaw(); toast("Проект загружен"); } catch (error) { toast(`Ошибка: ${error.message}`); } event.target.value = ""; });
    $("preview-data-button").addEventListener("click", () => { renderSamples(); $("sample-dialog").showModal(); });
    $("sample-fields").addEventListener("input", event => { if (!event.target.dataset.sample) return; state.samples[event.target.dataset.sample] = event.target.value; renderPreview(); });
    $("reset-samples").addEventListener("click", () => { state.samples = { ...DEFAULT_SAMPLES }; renderSamples(); renderPreview(); });
  }

  attachHandlers(); renderAll();
})();
