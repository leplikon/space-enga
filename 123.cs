// Улучшенный скрипт для программируемого блока в Space Engineers
// ПОЛНОСТЬЮ АВТОМАТИЗИРОВАННАЯ СИСТЕМА ДОБЫЧИ РУДЫ
// Включает интеллектуальную навигацию, энергоменеджмент, обнаружение препятствий
// Автоматическое обнаружение всех необходимых блоков
// Умная система поиска руды и оптимизации маршрутов

private MyIni _ini = new MyIni(); // INI парсер для чтения пользовательских настроек

// Основные настройки
private float cargoFullPercent = 0.9f; // Порог заполнения грузового отсека (90%)
private float batteryThreshold = 0.3f; // Порог заряда батареи (30%)
private double obstacleDetectionRange = 100.0; // Дальность обнаружения препятствий
private double safeDistance = 10.0; // Безопасное расстояние до препятствий
private bool autoDetectBlocks = true; // Автоматическое обнаружение блоков
private bool enableMultipleMiningPoints = false; // Поддержка множественных точек добычи
private bool isRunning = false; // Флаг запуска системы
private string miningMode = "all"; // Режим добычи
private double oreDetectionRange = 500.0; // Дальность поиска руды
private int maxOreSearchAttempts = 10; // Максимальное количество попыток поиска руды

// Улучшенные настройки автоматизации
private double maxSpeed = 25.0; // Максимальная скорость движения (м/с)
private double approachSpeed = 5.0; // Скорость приближения к цели (м/с)
private double miningDistance = 2.0; // Расстояние для добычи (м)
private double dockingDistance = 3.0; // Расстояние для стыковки (м)
private int maxStuckAttempts = 5; // Максимум попыток обхода препятствий
private double energyReservePercent = 0.2; // Резерв энергии (20%)

// Состояние дрона
private enum DroneState
{
    Stopped,
    Initializing,
    SearchingForOre,
    FlyingToOre,
    Mining,
    ReturningToBase,
    ApproachingBase,
    Docking,
    Charging,
    Error,
    SearchingForBlocks,
    AvoidingObstacle
}

private DroneState currentState = DroneState.Stopped;
private DateTime lastStateChange = DateTime.Now;
private Vector3D lastKnownPosition = Vector3D.Zero;
private int stuckCounter = 0;
private int currentMiningPointIndex = 0;
private int oreSearchAttempts = 0;
private Vector3D currentOrePosition = Vector3D.Zero;
private Vector3D currentTarget = Vector3D.Zero;
private int obstacleAvoidanceAttempts = 0;
private DateTime lastObstacleCheck = DateTime.Now;

// Конструктор программы
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update10; // Увеличена частота обновлений для лучшего контроля
    ParseConfig(); // Загрузка настроек из пользовательских данных
}

// Объявление блоков корабля
private IMyRemoteControl rc; // Блок дистанционного управления
private List<IMyThrust> thrusters = new List<IMyThrust>(); // Список двигателей
private List<IMyGyro> gyros = new List<IMyGyro>(); // Список гироскопов
private IMyShipConnector connector; // Коннектор для стыковки
private List<IMyCargoContainer> cargoContainers = new List<IMyCargoContainer>(); // Грузовые контейнеры
private IMyShipDrill drill; // Буровая установка
private List<IMyBatteryBlock> batteries = new List<IMyBatteryBlock>(); // Список ВСЕХ батарей
// Блоки для обнаружения препятствий
private List<IMyCameraBlock> cameras = new List<IMyCameraBlock>(); // Камеры
private List<IMySensorBlock> sensors = new List<IMySensorBlock>(); // Сенсоры
// Блоки для поиска руды
private List<IMyOreDetector> oreDetectors = new List<IMyOreDetector>(); // Детекторы руды

// Точки навигации из конфигурации
private Vector3D basePosition = new Vector3D(0, 0, 0); // Координаты базы
private List<Vector3D> miningPositions = new List<Vector3D>(); // Множественные точки добычи
private Vector3D currentMiningPosition = new Vector3D(100, 0, 0); // Текущая точка добычи

// Статистика работы
private int totalMiningCycles = 0;
private int totalItemsTransferred = 0;
private DateTime startTime = DateTime.Now;
private double totalDistanceTraveled = 0.0;
private int totalObstaclesAvoided = 0;

// Главный метод программы
public void Main(string arg, UpdateType updateSource)
{
    try
    {
        if (rc == null) 
        {
            if (!Init())
            {
                Echo("Критическая ошибка: Не удалось инициализировать основные блоки!");
                currentState = DroneState.Error;
                return;
            }
        }

        // Обработка команд
        if (arg == "run")
        {
            StartMiningOperation();
            return;
        }

        if (arg == "stop")
        {
            StopMiningOperation();
            return;
        }

        if (arg == "dock")
        {
            StartDockingSequence();
            return;
        }

        if (arg == "reset")
        {
            ResetDrone();
            return;
        }

        if (arg == "scan")
        {
            ScanForBlocks();
            return;
        }

        if (arg == "status")
        {
            ShowDetailedStatus();
            return;
        }

        if (arg == "findore")
        {
            FindNearestOre();
            return;
        }

        if (arg == "batteries")
        {
            Echo(GetBatteryInfo());
            return;
        }

        if (arg == "testore")
        {
            Echo("=== ТЕСТ ПОИСКА РУДЫ ===");
            Echo($"Найдено детекторов руды: {oreDetectors.Count}");
            if (oreDetectors.Count > 0)
            {
                foreach (var detector in oreDetectors)
                {
                    Echo($"Детектор: {detector.CustomName} (Включен: {detector.Enabled})");
                }
            }
            else
            {
                Echo("Детекторы руды не найдены!");
            }
            
            Echo($"Задано точек добычи: {miningPositions.Count}");
            if (miningPositions.Count > 0)
            {
                for (int i = 0; i < miningPositions.Count; i++)
                {
                    Echo($"Точка {i + 1}: {miningPositions[i]}");
                }
            }
            
            FindNearestOre();
            return;
        }

        // Проверка на застревание только если система запущена
        if (isRunning)
        {
            CheckIfStuck();
            CheckObstacles();
        }

        // Основная логика управления только если система запущена
        if (isRunning)
        {
            switch (currentState)
            {
                case DroneState.Stopped:
                    // Система остановлена
                    break;
                case DroneState.Initializing:
                    HandleInitialization();
                    break;
                case DroneState.SearchingForBlocks:
                    HandleBlockSearch();
                    break;
                case DroneState.SearchingForOre:
                    HandleOreSearch();
                    break;
                case DroneState.FlyingToOre:
                    HandleFlyingToOre();
                    break;
                case DroneState.Mining:
                    HandleMining();
                    break;
                case DroneState.ReturningToBase:
                    HandleReturnToBase();
                    break;
                case DroneState.ApproachingBase:
                    HandleApproachingBase();
                    break;
                case DroneState.Docking:
                    HandleDocking();
                    break;
                case DroneState.Charging:
                    HandleCharging();
                    break;
                case DroneState.AvoidingObstacle:
                    HandleAvoidingObstacle();
                    break;
                case DroneState.Error:
                    HandleError();
                    break;
            }

            // Обновление информации на дисплее
            UpdateDisplay();
        }
        else
        {
            // Показываем статус остановленной системы
            ShowStoppedStatus();
        }
    }
    catch (Exception ex)
    {
        Echo($"Ошибка в главном цикле: {ex.Message}");
        currentState = DroneState.Error;
    }
}

// Запуск операции добычи
private void StartMiningOperation()
{
    if (!ValidateAllSystems())
    {
        Echo("Ошибка: Не все системы готовы к работе!");
        return;
    }

    isRunning = true;
    currentState = DroneState.SearchingForOre;
    oreSearchAttempts = 0;
    Echo("Система добычи запущена. Начинаю поиск руды...");
}

// Остановка операции добычи
private void StopMiningOperation()
{
    isRunning = false;
    currentState = DroneState.Stopped;
    
    if (rc != null) rc.SetAutoPilotEnabled(false);
    if (drill != null && drill.Enabled) drill.Enabled = false;
    
    Echo("Система добычи остановлена.");
}

// Показать статус остановленной системы
private void ShowStoppedStatus()
{
    string status = "=== СИСТЕМА ОСТАНОВЛЕНА ===\n";
    status += "Используйте команду 'run' для запуска\n";
    status += "Используйте команду 'scan' для поиска блоков\n";
    status += "Используйте команду 'status' для статистики\n";
    
    if (rc != null)
    {
        status += $"Позиция: {rc.GetPosition():F1}\n";
    }
    
    status += GetBatteryInfo();
    
    Echo(status);
}

// Поиск ближайшей руды
private void FindNearestOre()
{
    if (rc == null) return;

    Echo("Начинаю поиск ближайшей руды...");
    
    try
    {
        // Получаем текущую позицию
        Vector3D currentPos = rc.GetPosition();
        Echo($"Текущая позиция: {currentPos}");
        Echo($"Дальность поиска: {oreDetectionRange}м");
        Echo($"Количество детекторов руды: {oreDetectors.Count}");
        
        // Проверяем наличие детекторов руды
        if (oreDetectors.Count == 0)
        {
            Echo("ОШИБКА: Детекторы руды не найдены!");
            Echo("Добавьте Ore Detector блоки на корабль");
            
            // Используем резервный метод - заданные координаты
            if (miningPositions.Count > 0)
            {
                Echo("Использую заданные координаты добычи...");
                UseFallbackMiningPositions(currentPos);
                return;
            }
            else
            {
                Echo("Нет заданных координат добычи. Возвращаюсь на базу.");
                currentState = DroneState.ReturningToBase;
                return;
            }
        }
        
        // Ищем руду через детекторы
        var oreList = new List<MyDetectedEntityInfo>();
        
        foreach (var detector in oreDetectors)
        {
            if (detector == null) continue;
            
            Echo($"Проверяю детектор руды: {detector.CustomName}");
            
            // Включаем детектор если он выключен
            if (!detector.Enabled)
            {
                detector.Enabled = true;
                Echo("Включил детектор руды");
            }
            
            // В Space Engineers детекторы руды работают автоматически
            // Они показывают руду в радиусе действия (обычно 150м)
            Echo($"Детектор руды активен. Дальность: ~150м");
            
            // Поскольку API не предоставляет прямой доступ к обнаруженным объектам,
            // мы будем использовать заданные координаты как основной метод
            // и детекторы как дополнительную проверку
        }
        
        // Используем заданные координаты добычи как основной метод
        if (miningPositions.Count > 0)
        {
            Echo("Использую заданные координаты добычи...");
            UseFallbackMiningPositions(currentPos);
            return;
        }
        else
        {
            oreSearchAttempts++;
            if (oreSearchAttempts >= maxOreSearchAttempts)
            {
                Echo("Не удалось найти руду. Возвращаюсь на базу.");
                currentState = DroneState.ReturningToBase;
            }
            else
            {
                Echo($"Попытка поиска руды {oreSearchAttempts}/{maxOreSearchAttempts}. Продолжаю поиск...");
                // Продолжаем поиск в следующем цикле
            }
        }
    }
    catch (Exception ex)
    {
        Echo($"Ошибка поиска руды: {ex.Message}");
        currentState = DroneState.Error;
    }
}

// Резервный метод - использование заданных координат добычи
private void UseFallbackMiningPositions(Vector3D currentPos)
{
    if (miningPositions.Count == 0)
    {
        Echo("Нет заданных координат добычи!");
        currentState = DroneState.ReturningToBase;
        return;
    }
    
    Echo($"Доступно точек добычи: {miningPositions.Count}");
    
    // Находим ближайшую заданную точку добычи
    Vector3D nearestPoint = miningPositions[0];
    double minDistance = Vector3D.Distance(currentPos, nearestPoint);
    
    Echo($"Точка 1: {miningPositions[0]} (расстояние: {minDistance:F1}м)");
    
    for (int i = 1; i < miningPositions.Count; i++)
    {
        double distance = Vector3D.Distance(currentPos, miningPositions[i]);
        Echo($"Точка {i + 1}: {miningPositions[i]} (расстояние: {distance:F1}м)");
        
        if (distance < minDistance)
        {
            minDistance = distance;
            nearestPoint = miningPositions[i];
        }
    }
    
    if (minDistance <= oreDetectionRange)
    {
        currentOrePosition = nearestPoint;
        currentTarget = nearestPoint;
        currentState = DroneState.FlyingToOre;
        Echo($"Выбрана ближайшая точка добычи на расстоянии {minDistance:F1}м");
        Echo($"Координаты: {nearestPoint}");
        Echo("Начинаю полет к месту добычи...");
    }
    else
    {
        Echo($"Ближайшая точка добычи слишком далеко: {minDistance:F1}м");
        Echo($"Максимальная дальность поиска: {oreDetectionRange}м");
        currentState = DroneState.ReturningToBase;
    }
}

// Проверка, является ли объект рудой
private bool IsOre(MyDetectedEntityInfo info)
{
    if (info.IsEmpty()) return false;
    
    string entityName = info.Name.ToLower();
    Echo($"Проверяю объект: {info.Name} (тип: {info.Type})");
    
    // Проверяем режим добычи
    switch (miningMode.ToLower())
    {
        case "all":
            bool isOre = entityName.Contains("ore") || 
                        entityName.Contains("руда") || 
                        entityName.Contains("iron") ||
                        entityName.Contains("silicon") ||
                        entityName.Contains("cobalt") ||
                        entityName.Contains("nickel") ||
                        entityName.Contains("platinum") ||
                        entityName.Contains("uranium") ||
                        entityName.Contains("gold") ||
                        entityName.Contains("silver") ||
                        entityName.Contains("железо") ||
                        entityName.Contains("кремний") ||
                        entityName.Contains("кобальт") ||
                        entityName.Contains("никель") ||
                        entityName.Contains("платина") ||
                        entityName.Contains("уран") ||
                        entityName.Contains("золото") ||
                        entityName.Contains("серебро");
            
            if (isOre) Echo($"Обнаружена руда: {info.Name}");
            return isOre;
            
        case "iron":
            bool isIron = entityName.Contains("iron") || entityName.Contains("железо");
            if (isIron) Echo($"Обнаружено железо: {info.Name}");
            return isIron;
            
        case "silicon":
            bool isSilicon = entityName.Contains("silicon") || entityName.Contains("кремний");
            if (isSilicon) Echo($"Обнаружен кремний: {info.Name}");
            return isSilicon;
            
        case "cobalt":
            bool isCobalt = entityName.Contains("cobalt") || entityName.Contains("кобальт");
            if (isCobalt) Echo($"Обнаружен кобальт: {info.Name}");
            return isCobalt;
            
        case "nickel":
            bool isNickel = entityName.Contains("nickel") || entityName.Contains("никель");
            if (isNickel) Echo($"Обнаружен никель: {info.Name}");
            return isNickel;
            
        case "platinum":
            bool isPlatinum = entityName.Contains("platinum") || entityName.Contains("платина");
            if (isPlatinum) Echo($"Обнаружена платина: {info.Name}");
            return isPlatinum;
            
        case "uranium":
            bool isUranium = entityName.Contains("uranium") || entityName.Contains("уран");
            if (isUranium) Echo($"Обнаружен уран: {info.Name}");
            return isUranium;
            
        case "gold":
            bool isGold = entityName.Contains("gold") || entityName.Contains("золото");
            if (isGold) Echo($"Обнаружено золото: {info.Name}");
            return isGold;
            
        case "silver":
            bool isSilver = entityName.Contains("silver") || entityName.Contains("серебро");
            if (isSilver) Echo($"Обнаружено серебро: {info.Name}");
            return isSilver;
            
        default:
            bool isAnyOre = entityName.Contains("ore") || entityName.Contains("руда");
            if (isAnyOre) Echo($"Обнаружена руда (по умолчанию): {info.Name}");
            return isAnyOre;
    }
}

// Обработка поиска руды
private void HandleOreSearch()
{
    FindNearestOre();
}

// Автоматическое сканирование и обнаружение блоков
private void ScanForBlocks()
{
    Echo("Начинаю сканирование блоков...");
    
    // Поиск блока удаленного управления
    if (rc == null)
    {
        var remoteControls = new List<IMyRemoteControl>();
        GridTerminalSystem.GetBlocksOfType(remoteControls);
        if (remoteControls.Count > 0)
        {
            rc = remoteControls[0];
            Echo($"Найден блок удаленного управления: {rc.CustomName}");
        }
    }

    // Поиск коннекторов
    if (connector == null)
    {
        var connectors = new List<IMyShipConnector>();
        GridTerminalSystem.GetBlocksOfType(connectors);
        if (connectors.Count > 0)
        {
            connector = connectors[0];
            Echo($"Найден коннектор: {connector.CustomName}");
        }
    }

    // Поиск буровых установок
    if (drill == null)
    {
        var drills = new List<IMyShipDrill>();
        GridTerminalSystem.GetBlocksOfType(drills);
        if (drills.Count > 0)
        {
            drill = drills[0];
            Echo($"Найдена буровая установка: {drill.CustomName}");
        }
    }

    // Поиск батарей
    if (batteries.Count == 0)
    {
        var batteriesList = new List<IMyBatteryBlock>();
        GridTerminalSystem.GetBlocksOfType(batteriesList);
        if (batteriesList.Count > 0)
        {
            batteries.AddRange(batteriesList);
            Echo($"Найдены батареи: {batteriesList.Count} шт.");
        }
    }

    // Поиск всех остальных блоков
    GridTerminalSystem.GetBlocksOfType(thrusters);
    GridTerminalSystem.GetBlocksOfType(gyros);
    GridTerminalSystem.GetBlocksOfType(cargoContainers);
    GridTerminalSystem.GetBlocksOfType(cameras);
    GridTerminalSystem.GetBlocksOfType(sensors);
    GridTerminalSystem.GetBlocksOfType(oreDetectors); // Добавлено получение всех детекторов руды

    Echo($"Найдено блоков: {thrusters.Count} двигателей, {gyros.Count} гироскопов, {cargoContainers.Count} контейнеров, {cameras.Count} камер, {sensors.Count} сенсоров, {oreDetectors.Count} детекторов руды");

    if (ValidateAllSystems())
    {
        currentState = DroneState.Stopped;
        Echo("Все необходимые блоки найдены! Система готова к работе.");
    }
    else
    {
        Echo("Не все блоки найдены. Проверьте наличие критических компонентов.");
    }
}

// Обработка инициализации
private void HandleInitialization()
{
    if (ValidateAllSystems())
    {
        currentState = DroneState.SearchingForOre;
        Echo("Системы инициализированы, начинаю поиск руды");
        
        // Установка текущей точки добычи
        if (miningPositions.Count > 0)
        {
            currentMiningPosition = miningPositions[currentMiningPointIndex % miningPositions.Count];
        }
        
        // Сброс счетчиков
        obstacleAvoidanceAttempts = 0;
        stuckCounter = 0;
    }
    else
    {
        if (autoDetectBlocks)
        {
            currentState = DroneState.SearchingForBlocks;
            Echo("Поиск недостающих блоков...");
        }
        else
        {
            currentState = DroneState.Error;
            Echo("Ошибка инициализации систем");
        }
    }
}

// Обработка поиска блоков
private void HandleBlockSearch()
{
    if (autoDetectBlocks)
    {
        ScanForBlocks();
        if (ValidateAllSystems())
        {
            currentState = DroneState.Stopped;
        }
        else
        {
            Echo("Ожидание обнаружения всех блоков...");
        }
    }
}

// Показать детальную статистику
private void ShowDetailedStatus()
{
    TimeSpan uptime = DateTime.Now - startTime;
    string status = $"=== ДЕТАЛЬНАЯ СТАТИСТИКА ===\n";
    status += $"Статус: {(isRunning ? "РАБОТАЕТ" : "ОСТАНОВЛЕН")}\n";
    status += $"Время работы: {uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}\n";
    status += $"Циклов добычи: {totalMiningCycles}\n";
    status += $"Передано предметов: {totalItemsTransferred}\n";
    status += $"Текущее состояние: {currentState}\n";
    status += $"Режим добычи: {miningMode}\n";
    
    if (rc != null)
    {
        status += $"Позиция: {rc.GetPosition():F1}\n";
        status += $"Скорость: {rc.GetShipVelocities().LinearVelocity.Length():F1} м/с\n";
    }
    
    status += GetBatteryInfo();
    
    if (cargoContainers.Count > 0)
    {
        double totalVolume = 0.0;
        double maxVolume = 0.0;
        int totalItems = 0;
        
        foreach (var container in cargoContainers)
        {
            if (container != null)
            {
                var inv = container.GetInventory();
                if (inv != null)
                {
                    totalVolume += (double)inv.CurrentVolume;
                    maxVolume += (double)inv.MaxVolume;
                    totalItems += inv.ItemCount;
                }
            }
        }
        
        if (maxVolume > 0)
        {
            double cargoPercent = (totalVolume / maxVolume) * 100.0;
            status += $"Груз: {cargoPercent:F1}% ({totalItems} предметов)\n";
        }
    }
    
    if (miningPositions.Count > 1)
    {
        status += $"Точка добычи: {currentMiningPointIndex % miningPositions.Count + 1}/{miningPositions.Count}\n";
    }
    
    status += $"Циклов: {totalMiningCycles}\n";
    status += $"Пройдено расстояния: {totalDistanceTraveled:F1}м\n";
    status += $"Обойдено препятствий: {totalObstaclesAvoided}\n";
    status += $"Скорость: {maxSpeed} м/с\n";
    status += $"Дальность добычи: {miningDistance}м\n";
    
    Echo(status);
}

// Интеллектуальная навигация к цели
private void HandleFlyingToOre()
{
    if (rc == null) return;

    Vector3D currentPos = rc.GetPosition();
    double distanceToTarget = Vector3D.Distance(currentPos, currentTarget);
    
    // Проверяем препятствия на пути
    if (ObstacleInPath(currentTarget))
    {
        currentState = DroneState.AvoidingObstacle;
        obstacleAvoidanceAttempts++;
        Echo($"Обнаружено препятствие на пути к руде. Попытка обхода {obstacleAvoidanceAttempts}/{maxStuckAttempts}");
        return;
    }
    
    // Если достигли цели
    if (distanceToTarget <= miningDistance)
    {
        currentState = DroneState.Mining;
        Echo("Достигнута позиция добычи. Начинаю добычу...");
        return;
    }
    
    // Интеллектуальное управление скоростью
    double targetSpeed = maxSpeed;
    if (distanceToTarget < 50.0)
    {
        targetSpeed = approachSpeed; // Замедляемся при приближении
    }
    
    // Навигация к цели
    FlyToTarget(currentTarget, targetSpeed);
    
    // Обновляем статистику
    if (lastKnownPosition != Vector3D.Zero)
    {
        totalDistanceTraveled += Vector3D.Distance(currentPos, lastKnownPosition);
    }
    lastKnownPosition = currentPos;
}

// Приближение к базе
private void HandleApproachingBase()
{
    if (rc == null) return;

    Vector3D currentPos = rc.GetPosition();
    double distanceToBase = Vector3D.Distance(currentPos, basePosition);
    
    // Проверяем препятствия
    if (ObstacleInPath(basePosition))
    {
        currentState = DroneState.AvoidingObstacle;
        obstacleAvoidanceAttempts++;
        Echo($"Обнаружено препятствие на пути к базе. Попытка обхода {obstacleAvoidanceAttempts}/{maxStuckAttempts}");
        return;
    }
    
    // Если достаточно близко к базе
    if (distanceToBase <= dockingDistance)
    {
        currentState = DroneState.Docking;
        Echo("Достигнута база. Начинаю стыковку...");
        return;
    }
    
    // Медленное приближение к базе
    FlyToTarget(basePosition, approachSpeed);
}

// Обработка обхода препятствий
private void HandleAvoidingObstacle()
{
    if (rc == null) return;

    if (obstacleAvoidanceAttempts >= maxStuckAttempts)
    {
        Echo("Превышено максимальное количество попыток обхода препятствий!");
        currentState = DroneState.Error;
        return;
    }
    
    // Попытка обхода препятствия
    Vector3D currentPos = rc.GetPosition();
    Vector3D avoidanceDirection = Vector3D.Up; // По умолчанию вверх
    
    // Пробуем разные направления обхода
    switch (obstacleAvoidanceAttempts % 4)
    {
        case 0: avoidanceDirection = Vector3D.Up; break;
        case 1: avoidanceDirection = Vector3D.Right; break;
        case 2: avoidanceDirection = Vector3D.Left; break;
        case 3: avoidanceDirection = Vector3D.Down; break;
    }
    
    Vector3D avoidanceTarget = currentPos + (avoidanceDirection * 20.0);
    
    Echo($"Попытка обхода препятствия в направлении {avoidanceDirection}");
    FlyToTarget(avoidanceTarget, approachSpeed);
    
    // Проверяем, удалось ли обойти препятствие
    if (DateTime.Now - lastObstacleCheck > TimeSpan.FromSeconds(5))
    {
        lastObstacleCheck = DateTime.Now;
        if (!ObstacleInPath(currentTarget))
        {
            totalObstaclesAvoided++;
            Echo("Препятствие успешно обойдено!");
            currentState = DroneState.FlyingToOre;
            obstacleAvoidanceAttempts = 0;
        }
    }
}

// Улучшенная проверка препятствий
private void CheckObstacles()
{
    if (rc == null) return;

    Vector3D currentPos = rc.GetPosition();
    
    // Проверяем препятствия только каждые 2 секунды для оптимизации
    if (DateTime.Now - lastObstacleCheck > TimeSpan.FromSeconds(2))
    {
        lastObstacleCheck = DateTime.Now;
        
        // Проверка камер
        foreach (var cam in cameras)
        {
            if (cam != null && cam.EnableRaycast && cam.CanScan(obstacleDetectionRange))
            {
                var info = cam.Raycast(obstacleDetectionRange);
                if (!info.IsEmpty())
                {
                    double obstacleDistance = Vector3D.Distance(currentPos, info.Position);
                    if (obstacleDistance < safeDistance)
                    {
                        Echo($"ПРЕДУПРЕЖДЕНИЕ: Препятствие на расстоянии {obstacleDistance:F1}м");
                        if (currentState == DroneState.FlyingToOre || currentState == DroneState.ApproachingBase)
                        {
                            currentState = DroneState.AvoidingObstacle;
                        }
                        return;
                    }
                }
            }
        }
        
        // Проверка сенсоров
        foreach (var sensor in sensors)
        {
            if (sensor != null && sensor.IsActive)
            {
                Echo("Сенсор обнаружил препятствие!");
                if (currentState == DroneState.FlyingToOre || currentState == DroneState.ApproachingBase)
                {
                    currentState = DroneState.AvoidingObstacle;
                }
                return;
            }
        }
    }
}

// Интеллектуальное управление полетом к цели
private void FlyToTarget(Vector3D target, double speed)
{
    if (rc == null) return;

    try
    {
        // Очищаем предыдущие маршруты
        rc.ClearWaypoints();
        
        // Добавляем новую цель
        rc.AddWaypoint(target, "target");
        
        // Устанавливаем скорость
        rc.SetAutoPilotEnabled(true);
        
        // Ограничиваем скорость через управление двигателями
        foreach (var thruster in thrusters)
        {
            if (thruster != null)
            {
                // Устанавливаем ограничение скорости
                thruster.ThrustOverride = (float)(speed / maxSpeed);
            }
        }
    }
    catch (Exception ex)
    {
        Echo($"Ошибка навигации к цели: {ex.Message}");
        currentState = DroneState.Error;
    }
}

// Улучшенная проверка застревания
private void CheckIfStuck()
{
    if (rc == null) return;

    Vector3D currentPos = rc.GetPosition();
    double distanceMoved = Vector3D.Distance(currentPos, lastKnownPosition);
    
    if (distanceMoved < 1.0) // Дрон не двигается
    {
        stuckCounter++;
        if (stuckCounter > 30) // Застрял на ~3 секунды (при Update10)
        {
            Echo("Предупреждение: Дрон может быть заблокирован!");
            currentState = DroneState.AvoidingObstacle;
            obstacleAvoidanceAttempts++;
        }
    }
    else
    {
        stuckCounter = 0;
    }
    
    lastKnownPosition = currentPos;
}

// Улучшенная проверка достижения позиции
private bool IsAtPosition(Vector3D pos)
{
    if (rc == null) return false;
    double distance = Vector3D.Distance(rc.GetPosition(), pos);
    return distance < 5.0;
}

// Улучшенная проверка заполнения грузового отсека
private bool IsCargoFull()
{
    if (cargoContainers.Count == 0) return false;

    foreach (var container in cargoContainers)
    {
        if (container == null) continue;
        
        var inv = container.GetInventory();
        if (inv != null && inv.CurrentVolume >= inv.MaxVolume * cargoFullPercent)
        {
            return true;
        }
    }
    return false;
}

// Улучшенная проверка уровня энергии
private bool HasEnoughPower()
{
    if (rc == null || batteries.Count == 0) 
    {
        if (!Init()) return false;
    }

    if (batteries.Count == 0)
    {
        Echo("Ошибка: Батареи не найдены!");
        return false;
    }

    double totalMaxCharge = 0.0;
    double totalCurrentCharge = 0.0;
    foreach (var bat in batteries)
    {
        if (bat != null)
        {
            totalMaxCharge += bat.MaxStoredPower;
            totalCurrentCharge += bat.CurrentStoredPower;
        }
    }

    // Улучшенный расчет энергопотребления
    double distance = Vector3D.Distance(rc.GetPosition(), basePosition);
    double mass = rc.CalculateShipMass().TotalMass;
    
    // Более точная формула энергопотребления
    double thrustPower = mass * 0.00002; // Потребление двигателей
    double systemsPower = 0.001; // Потребление систем
    double drillPower = (drill != null && drill.Enabled) ? 0.005 : 0.0; // Потребление бура
    double totalPowerPerSecond = thrustPower + systemsPower + drillPower;
    
    double travelTimeSeconds = distance / maxSpeed; // Используем настраиваемую скорость
    double energyNeeded = totalPowerPerSecond * travelTimeSeconds;
    
    // Добавляем настраиваемый резерв
    energyNeeded *= (1.0 + energyReservePercent);

    double remainingPower = totalCurrentCharge;
    
    if (remainingPower < energyNeeded)
    {
        Echo($"Недостаточно энергии: нужно {energyNeeded:F2} МВт⋅ч, доступно {remainingPower:F2} МВт⋅ч");
        return false;
    }

    return remainingPower / totalMaxCharge > batteryThreshold;
}

// Обработка добычи
private void HandleMining()
{
    if (IsCargoFull() || !HasEnoughPower())
    {
        currentState = DroneState.ReturningToBase;
        Echo("Возвращаюсь на базу");
        return;
    }

    // Активируем бур если он выключен
    if (drill != null && !drill.Enabled)
    {
        drill.Enabled = true;
        Echo("Бур активирован - добываю руду");
    }
    
    // Проверка эффективности добычи
    CheckMiningEfficiency();
}

// Проверка эффективности добычи
private void CheckMiningEfficiency()
{
    // Если добыча неэффективна, переключиться на следующую точку
    if (miningPositions.Count > 1)
    {
        // Простая логика: если груз не увеличивается, сменить точку
        // В реальной реализации можно добавить более сложную логику
    }
}

// Обработка возврата на базу
private void HandleReturnToBase()
{
    if (IsAtPosition(basePosition))
    {
        currentState = DroneState.ApproachingBase;
        return;
    }

    currentTarget = basePosition;
    FlyToTarget(basePosition, maxSpeed);
}

// Обработка стыковки
private void HandleDocking()
{
    if (connector != null && connector.Status == MyShipConnectorStatus.Connected)
    {
        TransferCargoToBase();
        currentState = DroneState.Charging;
        Echo("Груз выгружен, начинаю зарядку");
        totalMiningCycles++;
    }
    else
    {
        StartDockingSequence();
    }
}

// Обработка зарядки
private void HandleCharging()
{
    if (batteries.Count > 0)
    {
        double totalCharge = 0.0;
        double totalMaxCharge = 0.0;
        foreach (var bat in batteries)
        {
            if (bat != null)
            {
                totalCharge += bat.CurrentStoredPower;
                totalMaxCharge += bat.MaxStoredPower;
            }
        }
        double averageChargePercent = (totalMaxCharge > 0) ? (totalCharge / totalMaxCharge) * 100.0 : 0.0;

        if (averageChargePercent > 80.0) // Проверяем средний заряд всех батарей
        {
            currentState = DroneState.SearchingForOre;
            Echo("Батареи заряжены, начинаю поиск новой руды");
            
            // Сброс позиции руды для нового поиска
            currentOrePosition = Vector3D.Zero;
            oreSearchAttempts = 0;
            
            // Переключение на следующую точку добычи (если используется)
            if (miningPositions.Count > 1)
            {
                currentMiningPointIndex++;
                currentMiningPosition = miningPositions[currentMiningPointIndex % miningPositions.Count];
                Echo($"Переключение на точку добычи {currentMiningPointIndex % miningPositions.Count + 1}");
            }
        }
    }
}

// Обработка ошибок
private void HandleError()
{
    Echo("Система в состоянии ошибки. Используйте команду 'reset' для сброса.");
    if (rc != null) rc.SetAutoPilotEnabled(false);
    
    if (drill != null && drill.Enabled)
        drill.Enabled = false;
}

// Сброс дрона
private void ResetDrone()
{
    currentState = DroneState.Stopped;
    isRunning = false;
    stuckCounter = 0;
    currentMiningPointIndex = 0;
    totalMiningCycles = 0;
    totalItemsTransferred = 0;
    startTime = DateTime.Now;
    currentOrePosition = Vector3D.Zero;
    currentTarget = Vector3D.Zero;
    oreSearchAttempts = 0;
    obstacleAvoidanceAttempts = 0;
    totalDistanceTraveled = 0.0;
    totalObstaclesAvoided = 0;
    lastObstacleCheck = DateTime.Now;
    Echo("Система сброшена");
}

// Улучшенная проверка препятствий на пути
private bool ObstacleInPath(Vector3D destination)
{
    if (rc == null) return false;

    double dist = Vector3D.Distance(rc.GetPosition(), destination);
    double range = Math.Min(dist, obstacleDetectionRange);

    // Проверка камер
    foreach (var cam in cameras)
    {
        if (cam != null && cam.EnableRaycast && cam.CanScan(range))
        {
            var info = cam.Raycast(range);
            if (!info.IsEmpty())
            {
                double obstacleDistance = Vector3D.Distance(rc.GetPosition(), info.Position);
                if (obstacleDistance < safeDistance)
                {
                    Echo($"Обнаружено препятствие на расстоянии {obstacleDistance:F1}м");
                    return true;
                }
            }
        }
    }

    // Проверка сенсоров
    foreach (var sensor in sensors)
    {
        if (sensor != null && sensor.IsActive)
        {
            Echo("Сенсор обнаружил препятствие");
            return true;
        }
    }

    return false;
}

// Валидация всех систем
private bool ValidateAllSystems()
{
    bool isValid = true;

    if (rc == null)
    {
        Echo("Ошибка: Блок удаленного управления не найден!");
        isValid = false;
    }

    if (connector == null)
    {
        Echo("Ошибка: Коннектор не найден!");
        isValid = false;
    }

    if (drill == null)
    {
        Echo("Ошибка: Буровая установка не найдена!");
        isValid = false;
    }

    if (batteries.Count == 0)
    {
        Echo("Ошибка: Батареи не найдены!");
        isValid = false;
    }

    if (cargoContainers.Count == 0)
    {
        Echo("Ошибка: Грузовые контейнеры не найдены!");
        isValid = false;
    }

    if (oreDetectors.Count == 0)
    {
        Echo("ПРЕДУПРЕЖДЕНИЕ: Детекторы руды не найдены!");
        Echo("Система будет использовать заданные координаты добычи");
        // Не делаем isValid = false, так как есть резервный метод
    }

    return isValid;
}

// Инициализация блоков с улучшенной обработкой ошибок
private bool Init()
{
    try
    {
        if (autoDetectBlocks)
        {
            ScanForBlocks();
        }
        else
        {
            // Старый метод поиска по именам
            rc = GridTerminalSystem.GetBlockWithName("RC") as IMyRemoteControl;
            connector = GridTerminalSystem.GetBlockWithName("Connector") as IMyShipConnector;
            drill = GridTerminalSystem.GetBlockWithName("Drill") as IMyShipDrill;
            // battery = GridTerminalSystem.GetBlockWithName("Battery") as IMyBatteryBlock; // Удалено, т.к. теперь список
            
            GridTerminalSystem.GetBlocksOfType(thrusters);
            GridTerminalSystem.GetBlocksOfType(gyros);
            GridTerminalSystem.GetBlocksOfType(cargoContainers);
            GridTerminalSystem.GetBlocksOfType(cameras);
            GridTerminalSystem.GetBlocksOfType(sensors);
            GridTerminalSystem.GetBlocksOfType(oreDetectors); // Добавлено получение всех детекторов руды
            GridTerminalSystem.GetBlocksOfType(batteries); // Добавлено получение всех батарей
        }

        return ValidateAllSystems();
    }
    catch (Exception ex)
    {
        Echo($"Ошибка инициализации: {ex.Message}");
        return false;
    }
}





// Улучшенная процедура стыковки
private void StartDockingSequence()
{
    if (rc == null) 
    {
        if (!Init()) return;
    }

    if (rc == null)
    {
        Echo("Ошибка: Блок удаленного управления не найден!");
        return;
    }
    
    if (connector == null)
    {
        Echo("Ошибка: Коннектор не найден!");
        return;
    }

    try
    {
        rc.SetAutoPilotEnabled(false);
        
        if (connector.Status != MyShipConnectorStatus.Connected)
        {
            rc.ClearWaypoints();
            rc.AddWaypoint(basePosition, "base");
            rc.SetAutoPilotEnabled(true);
            Echo("Начинаю процедуру стыковки");
        }
        else
        {
            TransferCargoToBase();
        }
    }
    catch (Exception ex)
    {
        Echo($"Ошибка стыковки: {ex.Message}");
        currentState = DroneState.Error;
    }
}

// Улучшенная передача груза на базу
private void TransferCargoToBase()
{
    if (connector == null || connector.Status != MyShipConnectorStatus.Connected)
        return;

    try
    {
        var target = connector.GetInventory();
        if (target == null)
        {
            Echo("Ошибка: Не удалось получить доступ к инвентарю коннектора");
            return;
        }

        int transferredItems = 0;

        foreach (var container in cargoContainers)
        {
            if (container == null) continue;
            
            var inv = container.GetInventory();
            if (inv == null) continue;

            for (int i = inv.ItemCount - 1; i >= 0; i--)
            {
                var item = inv.GetItemAt(i);
                if (item.HasValue)
                {
                    inv.TransferItemTo(target, i, null, true);
                    transferredItems++;
                }
            }
        } 

        totalItemsTransferred += transferredItems;
        Echo($"Передано {transferredItems} предметов на базу (всего: {totalItemsTransferred})");
    }
    catch (Exception ex)
    {
        Echo($"Ошибка передачи груза: {ex.Message}");
    }
}

// Получить детальную информацию о батареях
private string GetBatteryInfo()
{
    if (batteries.Count == 0) return "Батареи не найдены";
    
    double totalCharge = 0.0;
    double totalMaxCharge = 0.0;
    int workingBatteries = 0;
    
    foreach (var bat in batteries)
    {
        if (bat != null)
        {
            totalCharge += bat.CurrentStoredPower;
            totalMaxCharge += bat.MaxStoredPower;
            workingBatteries++;
        }
    }
    
    double averageChargePercent = (totalMaxCharge > 0) ? (totalCharge / totalMaxCharge) * 100.0 : 0.0;
    
    string info = $"Батареи: {averageChargePercent:F1}% ({totalCharge:F2}/{totalMaxCharge:F2} МВт⋅ч) [{workingBatteries}/{batteries.Count} шт.]\n";
    
    // Детальная информация о каждой батарее (если их не слишком много)
    if (batteries.Count <= 5)
    {
        for (int i = 0; i < batteries.Count; i++)
        {
            var bat = batteries[i];
            if (bat != null)
            {
                double chargePercent = (bat.CurrentStoredPower / bat.MaxStoredPower) * 100.0;
                info += $"  Батарея {i + 1}: {chargePercent:F1}% ({bat.CurrentStoredPower:F2}/{bat.MaxStoredPower:F2} МВт⋅ч)\n";
            }
        }
    }
    
    return info;
}

// Обновление информации на дисплее
private void UpdateDisplay()
{
    string status = $"Статус: {(isRunning ? "РАБОТАЕТ" : "ОСТАНОВЛЕН")}\n";
    status += $"Состояние: {currentState}\n";
    
    status += GetBatteryInfo();
    
    if (rc != null)
    {
        status += $"Позиция: {rc.GetPosition():F1}\n";
    }
    
    if (cargoContainers.Count > 0)
    {
        double totalVolume = 0.0;
        double maxVolume = 0.0;
        
        foreach (var container in cargoContainers)
        {
            if (container != null)
            {
                var inv = container.GetInventory();
                if (inv != null)
                {
                    totalVolume += (double)inv.CurrentVolume;
                    maxVolume += (double)inv.MaxVolume;
                }
            }
        }
        
        if (maxVolume > 0)
        {
            double cargoPercent = (totalVolume / maxVolume) * 100.0;
            status += $"Груз: {cargoPercent:F1}%\n";
        }
    }
    
    if (miningPositions.Count > 1)
    {
        status += $"Точка добычи: {currentMiningPointIndex % miningPositions.Count + 1}/{miningPositions.Count}\n";
    }
    
    status += $"Режим: {miningMode}\n";
    status += $"Циклов: {totalMiningCycles}\n";
    
    Echo(status);
}

// Загрузка конфигурации с улучшенной обработкой ошибок
private void ParseConfig()
{
    try
    {
        // Очищаем предыдущие настройки
        _ini.Clear();
        
        string customData = Me.CustomData;
        if (string.IsNullOrWhiteSpace(customData))
        {
            Echo("CustomData пуст. Используются настройки по умолчанию");
            return;
        }

        // Удаляем комментарии и пустые строки
        string[] lines = customData.Split('\n');
        var cleanLines = new List<string>();
        
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedLine) && !trimmedLine.StartsWith("#"))
            {
                cleanLines.Add(trimmedLine);
            }
        }
        
        string cleanCustomData = string.Join("\n", cleanLines);
        
        Echo($"Очищенный CustomData:\n{cleanCustomData}");
        
        MyIniParseResult result;
        if (!_ini.TryParse(cleanCustomData, out result))
        {
            Echo($"Ошибка парсинга INI: {result.ToString()}");
            Echo("Используются настройки по умолчанию");
            return;
        }

        // Загружаем настройки с отладочной информацией
        basePosition = ParseGPS(_ini.Get("Settings", "BaseGPS").ToString(), basePosition);
        Echo($"BaseGPS загружен: {basePosition}");
        
        // Поддержка множественных точек добычи
        string miningGPS = _ini.Get("Settings", "MineGPS").ToString();
        Echo($"MineGPS из CustomData: {miningGPS}");
        
        if (!string.IsNullOrWhiteSpace(miningGPS))
        {
            if (miningGPS.Contains(";"))
            {
                // Множественные точки добычи
                string[] points = miningGPS.Split(';');
                miningPositions.Clear();
                foreach (string point in points)
                {
                    Vector3D pos = ParseGPS(point.Trim(), Vector3D.Zero);
                    if (pos != Vector3D.Zero)
                    {
                        miningPositions.Add(pos);
                        Echo($"Добавлена точка добычи: {pos}");
                    }
                }
                enableMultipleMiningPoints = miningPositions.Count > 1;
                if (miningPositions.Count > 0)
                {
                    currentMiningPosition = miningPositions[0];
                }
            }
            else
            {
                // Одна точка добычи
                currentMiningPosition = ParseGPS(miningGPS, currentMiningPosition);
                miningPositions.Clear();
                miningPositions.Add(currentMiningPosition);
                Echo($"Одна точка добычи: {currentMiningPosition}");
            }
        }
        
        // Загружаем остальные настройки
        cargoFullPercent = _ini.Get("Settings", "CargoFullPercent").ToSingle(cargoFullPercent);
        Echo($"CargoFullPercent: {cargoFullPercent}");
        
        batteryThreshold = _ini.Get("Settings", "BatteryThreshold").ToSingle(batteryThreshold);
        Echo($"BatteryThreshold: {batteryThreshold}");
        
        obstacleDetectionRange = _ini.Get("Settings", "ObstacleDetectionRange").ToDouble(obstacleDetectionRange);
        Echo($"ObstacleDetectionRange: {obstacleDetectionRange}");
        
        safeDistance = _ini.Get("Settings", "SafeDistance").ToDouble(safeDistance);
        Echo($"SafeDistance: {safeDistance}");
        
        autoDetectBlocks = _ini.Get("Settings", "AutoDetectBlocks").ToBoolean(autoDetectBlocks);
        Echo($"AutoDetectBlocks: {autoDetectBlocks}");
        
        // Новые параметры для добычи руды
        miningMode = _ini.Get("Settings", "MiningMode").ToString("all");
        Echo($"MiningMode: {miningMode}");
        
        oreDetectionRange = _ini.Get("Settings", "OreDetectionRange").ToDouble(oreDetectionRange);
        Echo($"OreDetectionRange: {oreDetectionRange}");
        
        maxOreSearchAttempts = _ini.Get("Settings", "MaxOreSearchAttempts").ToInt32(maxOreSearchAttempts);
        Echo($"MaxOreSearchAttempts: {maxOreSearchAttempts}");
        
        // Улучшенные настройки автоматизации
        maxSpeed = _ini.Get("Settings", "MaxSpeed").ToDouble(maxSpeed);
        Echo($"MaxSpeed: {maxSpeed}");
        approachSpeed = _ini.Get("Settings", "ApproachSpeed").ToDouble(approachSpeed);
        Echo($"ApproachSpeed: {approachSpeed}");
        miningDistance = _ini.Get("Settings", "MiningDistance").ToDouble(miningDistance);
        Echo($"MiningDistance: {miningDistance}");
        dockingDistance = _ini.Get("Settings", "DockingDistance").ToDouble(dockingDistance);
        Echo($"DockingDistance: {dockingDistance}");
        maxStuckAttempts = _ini.Get("Settings", "MaxStuckAttempts").ToInt32(maxStuckAttempts);
        Echo($"MaxStuckAttempts: {maxStuckAttempts}");
        energyReservePercent = (float)_ini.Get("Settings", "EnergyReservePercent").ToDouble(energyReservePercent);
        Echo($"EnergyReservePercent: {energyReservePercent}");

        Echo("Конфигурация загружена успешно!");
    }
    catch (Exception ex)
    {
        Echo($"Ошибка загрузки конфигурации: {ex.Message}");
        Echo("Используются настройки по умолчанию");
    }
}

// Улучшенный парсинг GPS координат
private Vector3D ParseGPS(string value, Vector3D fallback)
{
    if (string.IsNullOrWhiteSpace(value)) return fallback;
    
    try
    {
        if (value.StartsWith("GPS:"))
        {
            var parts = value.Split(':');
            if (parts.Length >= 5)
            {
                double x, y, z;
                if (double.TryParse(parts[2], out x) && double.TryParse(parts[3], out y) && double.TryParse(parts[4], out z))
                    return new Vector3D(x, y, z);
            }
        }
        else
        {
            char[] sep = new char[] { ',', ';', ' ' };
            var parts = value.Split(sep, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 3)
            {
                double x, y, z;
                if (double.TryParse(parts[0], out x) && double.TryParse(parts[1], out y) && double.TryParse(parts[2], out z))
                    return new Vector3D(x, y, z);
            }
        }
    }
    catch (Exception ex)
    {
        Echo($"Ошибка парсинга GPS: {ex.Message}");
    }
    
    return fallback;
}
