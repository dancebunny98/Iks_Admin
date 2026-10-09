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
    { key: "uncomm", title: "Снятие ограничения", group: "Наказания", route: "Punishments" },
    { key: "expired", title: "Истёк срок", group: "Наказания", route: "Punishments" },
    { key: "report", title: "Отчёт", group: "Система", route: "Reports" },
    { key: "anomaly", title: "Аномалия", group: "Система", route: "Anomalies" }
  ];
  const GROUPS = ["Варны игроков", "Варны модераторов", "Наказания", "Система"];
  const VARIABLES = [
    ["Игрок", "player", "Имя цели"], ["Игрок", "steamid", "SteamID64 цели"],
    ["Игрок", "playerurl", "Ссылка на Steam-профиль"], ["Игрок", "playerip", "IP цели, если известен"],
    ["Администратор", "admin", "Выдал или снял наказание"],
    ["Администратор", "adminsteamid", "SteamID64 действующего администратора"],
    ["Администратор", "adminurl", "Ссылка на его Steam-профиль"],
    ["Администратор", "adminid", "ID действующего администратора в IksAdmin"],
    ["Администратор", "targetadminid", "ID модератора, получившего варн"],
    ["ID наказания", "punishmentid", "ID записи; нет у кика и отчёта"],
    ["ID наказания", "banid", "ID бана: ban, unban, expired бан"],
    ["ID наказания", "commid", "ID ограничения чата: mute, gag, silence, uncomm"],
    ["ID наказания", "muteid", "ID мута, включая снятие/истечение"],
    ["ID наказания", "gagid", "ID гага, включая снятие/истечение"],
    ["ID наказания", "silenceid", "ID сайленса, включая снятие/истечение"],
    ["ID наказания", "warningid", "ID варна; у других событий 0"],
    ["ID наказания", "punishmentkind", "Исходный тип: ban, mute, gag, silence, warn"],
    ["ID наказания", "punishmentserverid", "ID сервера наказания; пусто для глобального"],
    ["ID наказания", "bantype", "Тип бана из БД: 0 Steam, 1 IP"],
    ["Наказание", "reason", "Причина"], ["Наказание", "removereason", "Причина снятия"],
    ["Наказание", "duration", "Длительность"], ["Наказание", "durationseconds", "Длительность в секундах"],
    ["Наказание", "durationminutes", "Длительность в целых минутах"],
    ["Наказание", "type", "Тип события"], ["Наказание", "source", "Источник варна"],
    ["Наказание", "message", "Текст нарушения"], ["Наказание", "originalissuer", "Первоначально выдал варн"],
    ["Наказание", "test", "Тестовый варн: true/false"],
    ["Наказание", "ismoderatorwarning", "Варн модератора: true/false"],
    ["Наказание", "emoji", "Значок стандартного шаблона"],
    ["Сервер", "servername", "Название сервера"], ["Сервер", "serverid", "ID сервера"],
    ["Сервер", "serverip", "IP:порт сервера"], ["Сервер", "online", "Игроков онлайн"],
    ["Время", "issuedat", "Дата выдачи"], ["Время", "expiresat", "Дата окончания"],
    ["Время", "removedat", "Дата снятия"], ["Время", "createdunix", "Unix для <t:...:F>"],
    ["Время", "expiresunix", "Unix окончания; 0 если без срока"],
    ["Время", "removedunix", "Unix снятия"], ["Время", "issuediso", "ISO выдачи для timestamp"],
    ["Время", "expiresiso", "ISO окончания"], ["Время", "removediso", "ISO снятия"],
    ["Время", "now", "Текущее время"], ["Время", "nowunix", "Текущее время Unix"],
    ["Время", "nowiso", "Текущее время ISO"],
    ["Отчёт", "period", "Период"], ["Отчёт", "author", "Автор отчёта"],
    ["Отчёт", "count", "Всего событий"], ["Отчёт", "bans", "Банов"],
    ["Отчёт", "kicks", "Киков"], ["Отчёт", "comms", "Наказаний чата"],
    ["Отчёт", "warnings", "Варнов"], ["Отчёт", "removedwarnings", "Снятых варнов"],
    ["Отчёт", "unbans", "Снятых банов"], ["Отчёт", "uncomms", "Снятых ограничений"],
    ["Отчёт", "threshold", "Порог аномалии"]
  ];
  const ALIASES = {
    target: "player", playername: "player", player_name: "player", steamid64: "steamid",
    playersteamid: "steamid", player_steamid: "steamid", player_url: "playerurl",
    issuer: "admin", adminname: "admin", admin_name: "admin", admin_steamid: "adminsteamid",
    admin_url: "adminurl", remove_reason: "removereason", duration_seconds: "durationseconds",
    durationminutes: "durationminutes", duration_minutes: "durationminutes", event: "type",
    warning_id: "warningid", original_issuer: "originalissuer", ismoderatorwarning: "ismoderatorwarning",
    punishment_id: "punishmentid", id: "punishmentid", punishment_kind: "punishmentkind",
    ban_id: "banid", ban_type: "bantype", comm_id: "commid", mute_id: "muteid",
    gag_id: "gagid", silence_id: "silenceid", punishment_server_id: "punishmentserverid",
    admin_id: "adminid", target_admin_id: "targetadminid", player_ip: "playerip", ip: "playerip",
    server: "servername", server_name: "servername", server_id: "serverid", server_ip: "serverip",
    issued_at: "issuedat", createdat: "issuedat", expires_at: "expiresat", removed_at: "removedat",
    created_unix: "createdunix", expires_unix: "expiresunix", removed_unix: "removedunix",
    issued_iso: "issuediso", expires_iso: "expiresiso", removed_iso: "removediso",
    nowunix: "nowunix", nowiso: "nowiso", emoji: "emoji", unbans: "unbans", uncomms: "uncomms"
  };
  const DEFAULT_SAMPLES = {
    player: "Player One", steamid: "76561198000000000", admin: "Moderator", adminsteamid: "76561198000000001",
    reason: "Нарушение правил чата", removereason: "Апелляция одобрена", warningid: "42", punishmentid: "128",
    adminid: "7", targetadminid: "23", playerip: "192.0.2.25", punishmentserverid: "1", bantype: "0",
    uncommkind: "mute", expiredkind: "ban",
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
    main: { Version: 1, Webhooks: { Punishments: "", Reports: "", Anomalies: "", Errors: "" }, Messages: {} },
    warnings: { Enabled: true, PlayerWebhook: "", ModeratorWebhook: "", Issued: true, Removed: true, Automatic: true, Test: true, Messages: {} },
    samples: { ...DEFAULT_SAMPLES }, selected: "player_warn_issued", drafts: { player_warn_issued: INITIAL_MESSAGE }, tab: "visual", lastInput: null
  };
  const $ = id => document.getElementById(id);
  const clone = value => structuredClone(value);
  const escapeHtml = value => String(value ?? "").replace(/[&<>"']/g, char => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[char]);
  const safeUrl = value => { try { const url = new URL(value); return url.protocol === "https:" || url.protocol === "http:" ? url.href : ""; } catch { return ""; } };
  const hexColor = value => `#${(Number(value) || 0).toString(16).padStart(6, "0").slice(-6)}`;
  const currentEvent = () => EVENTS.find(event => event.key === state.selected);
  const warningEvent = key => key.startsWith("player_warn_") || key.startsWith("moderator_warn_");
  const messageStore = () => warningEvent(state.selected) ? (state.warnings.Messages ||= {}) : state.main.Messages;
  const sharedWarningMessage = (key = state.selected) => {
    const template = state.warnings[key.endsWith("_issued") ? "IssuedTemplate" : "RemovedTemplate"];
    return plainObject(template) && (Object.hasOwn(template, "embeds") || Object.hasOwn(template, "content")) ? template : null;
  };
  const isConfigured = key => warningEvent(key)
    ? Object.hasOwn(state.warnings.Messages || {}, key) || (!sharedWarningMessage(key) && Object.hasOwn(state.main.Messages, key))
    : Object.hasOwn(state.main.Messages, key);
  const configured = () => isConfigured(state.selected);
  const defaultMessage = () => {
    const key = state.selected;
    const id = warningEvent(key) ? "warningid" :
      ({ ban: "banid", unban: "banid", mute: "muteid", gag: "gagid", silence: "silenceid", uncomm: "commid", expired: "punishmentid" })[key];
    const title = `${currentEvent().title}${id ? ` #{${id}}` : ""}`;
    const description = key === "report" ? "Событий: {count}\nБанов: {bans} · Ограничений чата: {comms}" :
      key === "anomaly" ? "Событий: {count}\nПорог: {threshold}" :
      key.endsWith("_removed") || key === "unban" || key === "uncomm"
        ? "**{admin}** снял наказание с **{player}**. {removereason}" :
        "**{player}**: {reason}";
    return { username: "IksAdmin Logs", content: "", embeds: [{ title, description, color: 5814783, fields: [] }] };
  };
  const currentMessage = () => messageStore()[state.selected] ??
    (warningEvent(state.selected) ? sharedWarningMessage() : null) ?? state.main.Messages[state.selected] ??
    (state.drafts[state.selected] ||= defaultMessage());
  const plainObject = value => value !== null && typeof value === "object" && !Array.isArray(value);
  function checkMessageShape(message) {
    if (!plainObject(message)) throw new Error("Ожидается объект сообщения Discord.");
    for (const key of ["username", "avatar_url", "content"]) {
      if (message[key] !== undefined && typeof message[key] !== "string") throw new Error(`${key} должен быть строкой.`);
    }
    for (const key of ["embeds", "components"]) {
      if (message[key] !== undefined && !Array.isArray(message[key])) throw new Error(`${key} должен быть массивом.`);
    }
    if (!(message.content || "").trim() && !(message.embeds || []).length)
      throw new Error("Сообщению нужен content или хотя бы один embed.");
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
      data[key] !== undefined && typeof data[key] !== "string") ||
      (data.Messages !== undefined && !plainObject(data.Messages))) throw new Error("Неверная структура warnings.json.");
    for (const [key, message] of Object.entries(data.Messages || {})) {
      if (!warningEvent(key)) throw new Error(`Неверный ключ сообщения варна: ${key}.`);
      checkMessageShape(message);
    }
    for (const key of ["IssuedTemplate", "RemovedTemplate"]) {
      const template = data[key];
      if (template !== undefined && !plainObject(template)) throw new Error(`${key} должен быть объектом.`);
      if (template && (Object.hasOwn(template, "embeds") || Object.hasOwn(template, "content"))) checkMessageShape(template);
    }
    return data;
  }
  function migrateWarningMessages(main, warnings) {
    warnings.Messages ||= {};
    for (const key of EVENTS.filter(event => warningEvent(event.key)).map(event => event.key)) {
      if (!Object.hasOwn(main.Messages || {}, key)) continue;
      if (!Object.hasOwn(warnings.Messages, key)) warnings.Messages[key] = main.Messages[key];
      delete main.Messages[key];
    }
  }
  const useMessage = () => {
    const store = messageStore();
    if (!Object.hasOwn(store, state.selected)) store[state.selected] = clone(currentMessage());
    if (warningEvent(state.selected)) delete state.main.Messages[state.selected];
    markChanged(); return store[state.selected];
  };
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
    const count = EVENTS.filter(event => isConfigured(event.key)).length;
    $("configured-count").textContent = `${count} / ${EVENTS.length}`;
    $("event-list").innerHTML = GROUPS.map(group => `<div class="event-group">${escapeHtml(group)}</div>` + EVENTS.filter(event => event.group === group).map(event =>
      `<button class="event-button ${state.selected === event.key ? "active" : ""}" data-event="${event.key}" type="button"><span class="event-dot ${isConfigured(event.key) ? "configured" : ""}"></span><span class="event-title">${escapeHtml(event.title)}</span></button>`).join("")).join("");
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
    const templateLabel = configured() ? "Свой шаблон" : warningEvent(state.selected) && sharedWarningMessage() ? "Общий шаблон варна" : "Черновик нового шаблона";
    $("route-label").textContent = `${templateLabel} · ${route.label} · ${route.disabled ? "отключено" : route.ready ? "webhook задан" : "webhook не задан"}`;
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
        ${(embed.fields || []).map((field, fieldIndex) => `<div class="field-row" data-field="${fieldIndex}"><div class="field-header"><div class="reorder-label"><button class="drag-handle" data-drag-field draggable="true" title="Перетащить поле" aria-label="Перетащить поле ${fieldIndex + 1}" type="button">⠿</button><span class="muted">Поле ${fieldIndex + 1}</span></div><div class="inline-actions"><button class="icon-action" data-move-field="-1" title="Поднять поле" aria-label="Поднять поле ${fieldIndex + 1}" type="button" ${fieldIndex === 0 ? "disabled" : ""}>↑</button><button class="icon-action" data-move-field="1" title="Опустить поле" aria-label="Опустить поле ${fieldIndex + 1}" type="button" ${fieldIndex === embed.fields.length - 1 ? "disabled" : ""}>↓</button><button class="icon-action danger" data-remove-field title="Удалить поле" aria-label="Удалить поле ${fieldIndex + 1}" type="button">×</button></div></div><div class="two-col"><label>Название<input data-field-key="name" maxlength="256" value="${escapeHtml(field.name)}"></label><label>Значение<input data-field-key="value" maxlength="1024" value="${escapeHtml(field.value)}"></label></div><label class="check-line"><input data-field-key="inline" type="checkbox" ${field.inline ? "checked" : ""}> В одну строку</label></div>`).join("")}</div>
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
  function moveItem(items, from, insertionIndex) {
    if (from < 0 || from >= items.length || insertionIndex < 0 || insertionIndex > items.length) return false;
    const to = insertionIndex > from ? insertionIndex - 1 : insertionIndex;
    if (to === from) return false;
    items.splice(to, 0, items.splice(from, 1)[0]);
    return true;
  }
  function renderButtonsEditor() {
    const buttons = allButtons();
    if (unsupportedComponents()) {
      $("buttons-editor").innerHTML = `<div class="empty-editor">В JSON есть интерактивные компоненты. Плагин поддерживает только ссылочные кнопки (style 5). Измените их на вкладке JSON.</div>`;
      return;
    }
    $("buttons-editor").innerHTML = buttons.length ? buttons.map((button, index) => `
      <div class="button-row" data-button="${index}"><div class="button-header"><div class="reorder-label"><button class="drag-handle" data-drag-button draggable="true" title="Перетащить кнопку" aria-label="Перетащить кнопку ${index + 1}" type="button">⠿</button><strong>Ссылка ${index + 1}</strong></div><div class="inline-actions"><button class="icon-action" data-move-button="-1" title="Поднять" aria-label="Поднять кнопку ${index + 1}" type="button" ${index === 0 ? "disabled" : ""}>↑</button><button class="icon-action" data-move-button="1" title="Опустить" aria-label="Опустить кнопку ${index + 1}" type="button" ${index === buttons.length - 1 ? "disabled" : ""}>↓</button><button class="icon-action danger" data-remove-button title="Удалить" aria-label="Удалить кнопку ${index + 1}" type="button">×</button></div></div><div class="two-col"><label>Текст<input data-button-key="label" maxlength="80" value="${escapeHtml(button.label)}"></label><label>HTTPS-ссылка<input data-button-key="url" value="${escapeHtml(button.url)}" placeholder="https://..."></label></div></div>`).join("") : `<div class="empty-editor">Нет кнопок. Для webhook доступны HTTPS-ссылки.</div>`;
  }
  function renderVariables() {
    const search = $("variable-search").value.trim().toLowerCase();
    const values = sampleValues();
    $("variable-list").innerHTML = [...new Set(VARIABLES.map(row => row[0]))].map(group => {
      const rows = VARIABLES.filter(row => row[0] === group && (!search || `${row[1]} ${row[2]} ${Object.keys(ALIASES).filter(alias => alias !== row[1] && ALIASES[alias] === row[1]).join(" ")}`.toLowerCase().includes(search)));
      return rows.length ? `<div class="variable-group"><h3>${group}</h3>${rows.map(row => {
        const aliases = Object.keys(ALIASES).filter(alias => alias !== row[1] && ALIASES[alias] === row[1]);
        const available = Object.hasOwn(values, row[1]) && String(values[row[1]]) !== "";
        return `<div class="variable-row"><button class="button small" data-variable="${row[1]}" type="button"><code>{${row[1]}}</code></button><span>${escapeHtml(row[2])}<small>${available ? "Пример: " + escapeHtml(String(values[row[1]]).slice(0, 70)) : "Для этого события пусто"}${aliases.length ? " · " + escapeHtml(aliases.map(alias => `{${alias}}`).join(", ")) : ""}</small></span></div>`;
      }).join("")}</div>` : "";
    }).join("") || `<div class="empty-editor">Ничего не найдено.</div>`;
  }
  function sampleValues() {
    const now = new Date();
    const key = state.selected;
    const isWarning = warningEvent(key), isReport = key === "report" || key === "anomaly";
    const isRemoval = key.endsWith("_removed") || key === "unban" || key === "uncomm";
    const commKinds = ["mute", "gag", "silence"];
    const kind = isWarning ? "warn" : key === "unban" ? "ban" :
      key === "uncomm" ? (commKinds.includes(state.samples.uncommkind) ? state.samples.uncommkind : "mute") :
      key === "expired" ? (["ban", ...commKinds].includes(state.samples.expiredkind) ? state.samples.expiredkind : "ban") : key;
    const isBan = kind === "ban", isComm = commKinds.includes(kind);
    const durationSeconds = isWarning || key === "kick" ? 0 : Number(state.samples.durationseconds || 0);
    const expires = new Date(now.getTime() + durationSeconds * 1000);
    const steam = key === "kick" ? "" : state.samples.steamid || "";
    const adminSteam = state.samples.adminsteamid || "";
    const currentTime = { now: now.toLocaleString("ru-RU"), nowunix: String(Math.floor(now.getTime() / 1000)), nowiso: now.toISOString() };
    const server = { servername: state.samples.servername, serverid: state.samples.serverid, serverip: state.samples.serverip };
    if (isReport) return {
      ...server, ...currentTime, period: state.samples.period, author: state.samples.author,
      count: state.samples.count, bans: state.samples.bans, kicks: state.samples.kicks,
      comms: state.samples.comms, warnings: state.samples.warnings, removedwarnings: state.samples.removedwarnings,
      unbans: state.samples.unbans, uncomms: state.samples.uncomms, threshold: state.samples.threshold
    };
    const values = {
      ...state.samples, ...server, ...currentTime, steamid: steam,
      playerurl: /^\d{17}$/.test(steam) ? `https://steamcommunity.com/profiles/${steam}` : "",
      adminurl: /^\d{17}$/.test(adminSteam) ? `https://steamcommunity.com/profiles/${adminSteam}` : "",
      playerip: isWarning || key === "kick" ? "" : state.samples.playerip,
      targetadminid: key.startsWith("moderator_warn_") ? state.samples.targetadminid : "",
      punishmentserverid: isBan || isComm ? state.samples.punishmentserverid : "",
      bantype: isBan ? state.samples.bantype : "",
      punishmentid: isWarning ? state.samples.warningid : isBan || isComm ? state.samples.punishmentid : "",
      punishmentkind: kind,
      banid: isBan ? state.samples.punishmentid : "",
      commid: isComm ? state.samples.punishmentid : "",
      muteid: kind === "mute" ? state.samples.punishmentid : "",
      gagid: kind === "gag" ? state.samples.punishmentid : "",
      silenceid: kind === "silence" ? state.samples.punishmentid : "",
      warningid: isWarning ? state.samples.warningid : "0",
      source: key.startsWith("moderator_warn_") ? "administrator" : isWarning ? state.samples.source : "",
      message: isWarning ? state.samples.message : "",
      originalissuer: isWarning ? state.samples.admin : "",
      test: isWarning ? state.samples.test : "false",
      duration: durationSeconds ? `${Math.round(durationSeconds / 60)} мин.` : "Навсегда",
      durationseconds: String(durationSeconds), durationminutes: String(Math.floor(durationSeconds / 60)),
      type: key.replace(/^(player|moderator)_/, ""), ismoderatorwarning: String(key.startsWith("moderator_")),
      issuedat: now.toLocaleString("ru-RU"), expiresat: durationSeconds ? expires.toLocaleString("ru-RU") : "Never",
      removedat: isRemoval ? now.toLocaleString("ru-RU") : "",
      createdunix: String(Math.floor(now.getTime() / 1000)), expiresunix: durationSeconds ? String(Math.floor(expires.getTime() / 1000)) : "0",
      removedunix: isRemoval ? String(Math.floor(now.getTime() / 1000)) : "",
      issuediso: now.toISOString(), expiresiso: durationSeconds ? expires.toISOString() : "",
      removediso: isRemoval ? now.toISOString() : "", emoji: "📋"
    };
    for (const key of ["period", "author", "count", "bans", "kicks", "comms", "warnings", "removedwarnings", "unbans", "uncomms", "threshold", "uncommkind", "expiredkind"]) delete values[key];
    return values;
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
    for (const [embedIndex, embed] of (message.embeds || []).entries()) {
      const title = expand(embed.title, values), description = expand(embed.description, values);
      const footer = expand(embed.footer?.text, values), author = expand(embed.author?.name, values);
      if (title.length > 256 || description.length > 4096 || footer.length > 2048 || author.length > 256) errors.push("Превышен лимит заголовка, описания, автора или подписи embed.");
      embedLength += title.length + description.length + footer.length + author.length;
      if ((embed.fields || []).length > 25) errors.push("В одном embed допускается не более 25 полей.");
      for (const [fieldIndex, field] of (embed.fields || []).entries()) {
        const name = expand(field.name, values), value = expand(field.value, values);
        const where = `Embed ${embedIndex + 1}, поле ${fieldIndex + 1}${name ? ` «${name.slice(0, 40)}»` : ""}`;
        if (!name.trim()) errors.push(`${where}: название пустое. Укажите название поля.`);
        else if (name.length > 256) errors.push(`${where}: название длиннее 256 символов.`);
        if (!value.trim()) {
          const empty = [...String(field.value ?? "").matchAll(placeholderPattern)]
            .map(match => match[0]).filter(token => {
              const key = token.slice(1, -1).toLowerCase();
              const canonical = ALIASES[key] || key;
              return Object.hasOwn(values, canonical) && !String(values[canonical]).trim();
            });
          errors.push(empty.length
            ? `${where}: ${[...new Set(empty)].join(", ")} пуст для события «${currentEvent().title}». Уберите это поле или добавьте постоянный текст.`
            : `${where}: значение пустое. Укажите текст или уберите поле.`);
        } else if (value.length > 1024) errors.push(`${where}: значение длиннее 1024 символов.`);
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
    if (unknown.size) errors.push(`Недоступные для события или неизвестные заполнители: ${[...unknown].join(", ")}.`);
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
      else if (warningEvent(state.selected) && parsed[state.selected.endsWith("_issued") ? "IssuedTemplate" : "RemovedTemplate"])
        message = parsed[state.selected.endsWith("_issued") ? "IssuedTemplate" : "RemovedTemplate"];
      else if (parsed[state.selected]) message = parsed[state.selected];
      messageStore()[state.selected] = checkMessageShape(message);
      if (warningEvent(state.selected)) delete state.main.Messages[state.selected];
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
    const names = { player: "Игрок", steamid: "SteamID64", playerip: "IP цели", admin: "Администратор", adminsteamid: "SteamID64 админа", adminid: "ID администратора", targetadminid: "ID модератора-цели", punishmentid: "ID наказания", warningid: "ID варна", punishmentserverid: "ID сервера наказания", bantype: "Тип бана (0 Steam, 1 IP)", reason: "Причина", removereason: "Причина снятия", source: "Источник", message: "Сообщение", servername: "Сервер", serverid: "ID сервера", serverip: "IP:порт", online: "Онлайн", durationseconds: "Длительность, сек", period: "Период", author: "Автор отчёта", count: "Событий", bans: "Банов", kicks: "Киков", comms: "Ограничений чата", warnings: "Варнов", removedwarnings: "Снятых варнов", threshold: "Порог", unbans: "Снятых банов", uncomms: "Снятых ограничений", test: "Тест" };
    const choice = (key, name, options) => `<label>${name}<select data-sample="${key}">${options.map(option => `<option value="${option}" ${state.samples[key] === option ? "selected" : ""}>${option}</option>`).join("")}</select></label>`;
    $("sample-fields").innerHTML = Object.entries(names).map(([key, name]) => `<label>${name}<input data-sample="${key}" value="${escapeHtml(state.samples[key])}"></label>`).join("") +
      choice("uncommkind", "Снятие ограничения: исходный тип", ["mute", "gag", "silence"]) +
      choice("expiredkind", "Истечение: исходный тип", ["ban", "mute", "gag", "silence"]);
  }
  function updateSetting(path, value) {
    const parts = path.split("."); const root = parts.shift(); let object = state[root];
    for (const part of parts.slice(0, -1)) object = object[part] ||= {};
    object[parts.at(-1)] = value;
    markChanged(); renderHeading(); renderPreview();
  }
  async function readJson(file) { return JSON.parse(await file.text()); }
  function attachReorder(container, kind) {
    const selector = kind === "field" ? "[data-field]" : "[data-button]";
    const handleSelector = kind === "field" ? "[data-drag-field]" : "[data-drag-button]";
    let dragged = null;
    const clearMarkers = () => container.querySelectorAll(".drag-source,.drop-before,.drop-after")
      .forEach(row => row.classList.remove("drag-source", "drop-before", "drop-after"));
    container.addEventListener("dragstart", event => {
      const handle = event.target.closest(handleSelector);
      if (!handle || (kind === "button" && unsupportedComponents())) return;
      const row = handle.closest(selector);
      dragged = { index: Number(row.dataset[kind]), embed: kind === "field" ? Number(row.closest("[data-embed]").dataset.embed) : null, row };
      event.dataTransfer.effectAllowed = "move";
      event.dataTransfer.setData("text/plain", kind);
      row.classList.add("drag-source");
    });
    container.addEventListener("dragover", event => {
      if (!dragged) return;
      const row = event.target.closest(selector);
      if (!row || (kind === "field" && Number(row.closest("[data-embed]").dataset.embed) !== dragged.embed)) return;
      event.preventDefault();
      event.dataTransfer.dropEffect = "move";
      clearMarkers();
      dragged.row.classList.add("drag-source");
      row.classList.add(event.clientY < row.getBoundingClientRect().top + row.getBoundingClientRect().height / 2 ? "drop-before" : "drop-after");
    });
    container.addEventListener("drop", event => {
      if (!dragged) return;
      const row = event.target.closest(selector);
      if (!row || (kind === "field" && Number(row.closest("[data-embed]").dataset.embed) !== dragged.embed)) return;
      event.preventDefault();
      const after = event.clientY >= row.getBoundingClientRect().top + row.getBoundingClientRect().height / 2;
      const target = Number(row.dataset[kind]) + Number(after);
      if (kind === "field") {
        const fields = clone(currentMessage().embeds[dragged.embed].fields);
        if (moveItem(fields, dragged.index, target)) {
          useMessage().embeds[dragged.embed].fields = fields;
          renderEmbedsEditor(); renderPreview(); renderHeading();
        }
      } else {
        const buttons = clone(allButtons());
        if (moveItem(buttons, dragged.index, target)) {
          setButtons(buttons); renderButtonsEditor(); renderPreview(); renderHeading();
        }
      }
      dragged = null;
      clearMarkers();
    });
    container.addEventListener("dragend", () => { dragged = null; clearMarkers(); });
  }
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
      const button = event.target.closest("button"); if (!button || button.hasAttribute("data-drag-field")) return;
      const embedIndex = Number(button.closest("[data-embed]").dataset.embed), message = useMessage(), embeds = message.embeds;
      if (button.hasAttribute("data-remove-embed")) embeds.splice(embedIndex, 1);
      else if (button.hasAttribute("data-move-embed")) { const other = embedIndex + Number(button.dataset.moveEmbed); if (other < 0 || other >= embeds.length) return; [embeds[embedIndex], embeds[other]] = [embeds[other], embeds[embedIndex]]; }
      else if (button.hasAttribute("data-add-field")) { (embeds[embedIndex].fields ||= []).push({ name: "Название", value: "Значение", inline: false }); }
      else if (button.hasAttribute("data-remove-field")) embeds[embedIndex].fields.splice(Number(button.closest("[data-field]").dataset.field), 1);
      else if (button.hasAttribute("data-move-field")) {
        const index = Number(button.closest("[data-field]").dataset.field);
        if (!moveItem(embeds[embedIndex].fields, index, index + Number(button.dataset.moveField) + (Number(button.dataset.moveField) > 0 ? 1 : 0))) return;
      }
      else return;
      renderEmbedsEditor(); renderPreview(); renderHeading();
    });
    $("buttons-editor").addEventListener("click", event => {
      const button = event.target.closest("button"); if (!button || button.hasAttribute("data-drag-button") || unsupportedComponents()) return;
      const index = Number(button.closest("[data-button]").dataset.button), buttons = clone(allButtons());
      if (button.hasAttribute("data-remove-button")) buttons.splice(index, 1);
      else if (button.hasAttribute("data-move-button")) {
        if (!moveItem(buttons, index, index + Number(button.dataset.moveButton) + (Number(button.dataset.moveButton) > 0 ? 1 : 0))) return;
      }
      else return;
      setButtons(buttons); renderButtonsEditor(); renderPreview(); renderHeading();
    });
    $("add-embed").addEventListener("click", () => { const message = useMessage(); if ((message.embeds ||= []).length >= 10) return toast("Лимит: 10 embeds"); message.embeds.push({ title: "Новый embed", description: "", color: 5814783, fields: [] }); renderEmbedsEditor(); renderPreview(); renderHeading(); });
    $("add-button").addEventListener("click", () => { if (unsupportedComponents()) return toast("Сначала исправьте компоненты в JSON"); const buttons = clone(allButtons()); if (buttons.length >= 25) return toast("Лимит: 25 кнопок"); buttons.push({ type: 2, style: 5, label: "Ссылка", url: "https://example.com" }); setButtons(buttons); renderButtonsEditor(); renderPreview(); renderHeading(); });
    attachReorder($("embeds-editor"), "field");
    attachReorder($("buttons-editor"), "button");
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
    $("clear-message").addEventListener("click", () => { if (!confirm("Удалить свой шаблон этого события? Плагин использует общий шаблон варна или стандартный embed.")) return; delete messageStore()[state.selected]; if (warningEvent(state.selected)) delete state.main.Messages[state.selected]; delete state.drafts[state.selected]; $("raw-json").dataset.dirty = "false"; markChanged(); renderAll(); if (state.tab === "json") refreshRaw(); });
    $("copy-message").addEventListener("click", () => { if (savePendingRaw()) copyText(JSON.stringify(currentMessage(), null, 2)); });
    $("download-message").addEventListener("click", () => { if (savePendingRaw()) download(`${state.selected}.json`, currentMessage()); });
    $("import-project").addEventListener("click", () => $("project-file").click());
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
    $("warnings-file").addEventListener("change", async event => { if (!event.target.files[0]) return; try { state.warnings = checkWarningsConfig(await readJson(event.target.files[0])); markChanged(); renderAll(); if (state.tab === "json") refreshRaw(); toast("Конфиг варнов загружен"); } catch (error) { toast(`Ошибка: ${error.message}`); } event.target.value = ""; });
    $("project-file").addEventListener("change", async event => { if (!event.target.files[0]) return; try { const data = await readJson(event.target.files[0]); if (data.format !== "iksadmin-discord-studio-v1") throw new Error("Неверный формат проекта"); checkMainConfig(data.main); checkWarningsConfig(data.warnings); state.main = data.main; state.main.Messages ||= {}; state.main.Webhooks ||= {}; state.warnings = data.warnings; migrateWarningMessages(state.main, state.warnings); state.samples = { ...DEFAULT_SAMPLES, ...(plainObject(data.samples) ? data.samples : {}) }; state.drafts = {}; $("raw-json").dataset.dirty = "false"; markChanged(); renderAll(); if (state.tab === "json") refreshRaw(); toast("Проект загружен"); } catch (error) { toast(`Ошибка: ${error.message}`); } event.target.value = ""; });
    $("preview-data-button").addEventListener("click", () => { renderSamples(); $("sample-dialog").showModal(); });
    $("sample-fields").addEventListener("input", event => { if (!event.target.dataset.sample) return; state.samples[event.target.dataset.sample] = event.target.value; renderPreview(); renderVariables(); });
    $("reset-samples").addEventListener("click", () => { state.samples = { ...DEFAULT_SAMPLES }; renderSamples(); renderPreview(); renderVariables(); });
  }

  attachHandlers(); renderAll();
})();
