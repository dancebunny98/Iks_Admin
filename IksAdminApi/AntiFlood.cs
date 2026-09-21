namespace IksAdminApi;

/// <summary>
/// Лёгкая защита от флуда (чат-спам и другие часто вызываемые команды), которая
/// намеренно НЕ обращается к БД в момент проверки — это как раз то, что нужно во время
/// самой атаки, когда лишний DB-запрос на каждое сообщение только усугубит лаги.
/// Вся статистика хранится в памяти и живёт, пока онлайн-игрок числится в словаре
/// (см. Cleanup, который надо вызывать при дисконнекте).
/// </summary>
public static class AntiFlood
{
    private static readonly Dictionary<string, Queue<DateTime>> Actions = new();
    private static readonly Dictionary<string, int> Violations = new();

    /// <summary>
    /// Регистрирует очередное действие игрока (например, сообщение в чат) и возвращает true,
    /// если за последние windowSeconds секунд действий стало больше, чем maxActions —
    /// то есть игрок флудит и это конкретное действие нужно заблокировать.
    /// </summary>
    public static bool IsFlooding(string steamId, int maxActions, double windowSeconds)
    {
        if (!Actions.TryGetValue(steamId, out var queue))
        {
            queue = new Queue<DateTime>();
            Actions[steamId] = queue;
        }

        var now = DateTime.UtcNow;
        queue.Enqueue(now);

        while (queue.Count > 0 && (now - queue.Peek()).TotalSeconds > windowSeconds)
        {
            queue.Dequeue();
        }

        return queue.Count > maxActions;
    }

    /// <summary>
    /// Фиксирует, что игрок словил очередное превышение лимита (для эскалации наказания:
    /// первый раз — просто блокируем действие, повторные — гаг/кик). Возвращает текущее
    /// число нарушений за это подключение.
    /// </summary>
    public static int RegisterViolation(string steamId)
    {
        Violations.TryGetValue(steamId, out var count);
        count++;
        Violations[steamId] = count;
        return count;
    }

    public static int GetViolations(string steamId)
    {
        return Violations.GetValueOrDefault(steamId, 0);
    }

    /// <summary>
    /// Вызывать при дисконнекте игрока — чтобы словари не росли бесконечно на сервере
    /// с большой ротацией игроков (утечка памяти в долгоживущем процессе).
    /// </summary>
    public static void Cleanup(string steamId)
    {
        Actions.Remove(steamId);
        Violations.Remove(steamId);
    }
}
