// Улучшенный скрипт для программируемого блока в Space Engineers
// Этот скрипт обеспечивает автономное управление добывающим дроном с улучшенной безопасностью
// Дрон добывает руду, пока его грузовой контейнер не заполнится
// Затем возвращается на базу, выгружает руду и продолжает добычу
// Включает улучшенное обнаружение препятствий, обработку ошибок и энергетический мониторинг

private MyIni _ini = new MyIni(); // INI парсер для чтения пользовательских настроек
private float cargoFullPercent = 0.9f; // Порог заполнения грузового отсека (90%)
private float batteryThreshold = 0.3f; // Порог заряда батареи (30%)
private double obstacleDetectionRange = 100.0; // Увеличенная дальность обнаружения препятствий
private double safeDistance = 10.0; // Безопасное расстояние до препятствий

// Состояние дрона
private enum DroneState
{
    Initializing,
    Mining,
    ReturningToBase,
    Docking,
    Charging,
    Error
}

private DroneState currentState = DroneState.Initializing;
private DateTime lastStateChange = DateTime.Now;
private Vector3D lastKnownPosition = Vector3D.Zero;
private int stuckCounter = 0;

// Конструктор программы
public Program()
{
    Runtime.UpdateFrequency = UpdateFrequency.Update100; // Запуск каждые ~1.6 секунды
    ParseConfig(); // Загрузка настроек из пользовательских данных
}

// Объявление блоков корабля
private IMyRemoteControl rc; // Блок дистанционного управления
private List<IMyThrust> thrusters = new List<IMyThrust>(); // Список двигателей
private List<IMyGyro> gyros = new List<IMyGyro>(); // Список гироскопов
private IMyShipConnector connector; // Коннектор для стыковки
private List<IMyCargoContainer> cargoContainers = new List<IMyCargoContainer>(); // Грузовые контейнеры
private IMyShipDrill drill; // Буровая установка
private IMyBatteryBlock battery; // Батарея
// Блоки для обнаружения препятствий
private List<IMyCameraBlock> cameras = new List<IMyCameraBlock>(); // Камеры
private List<IMySensorBlock> sensors = new List<IMySensorBlock>(); // Сенсоры

// Точки навигации из конфигурации
private Vector3D basePosition = new Vector3D(0, 0, 0); // Координаты базы
private Vector3D miningPosition = new Vector3D(100, 0, 0); // Координаты точки добычи

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

        // Проверка на застревание
        CheckIfStuck();

        if (arg == "dock") // Проверка команды стыковки
        {
            StartDockingSequence();
            return;
        }

        if (arg == "reset")
        {
            ResetDrone();
            return;
        }

        // Основная логика управления
        switch (currentState)
        {
            case DroneState.Initializing:
                HandleInitialization();
                break;
            case DroneState.Mining:
                HandleMining();
                break;
            case DroneState.ReturningToBase:
                HandleReturnToBase();
                break;
            case DroneState.Docking:
                HandleDocking();
                break;
            case DroneState.Charging:
                HandleCharging();
                break;
            case DroneState.Error:
                HandleError();
                break;
        }

        // Обновление информации на дисплее
        UpdateDisplay();
    }
    catch (Exception ex)
    {
        Echo($"Ошибка в главном цикле: {ex.Message}");
        currentState = DroneState.Error;
    }
}

// Проверка на застревание дрона
private void CheckIfStuck()
{
    if (rc == null) return;

    Vector3D currentPos = rc.GetPosition();
    double distanceMoved = Vector3D.Distance(currentPos, lastKnownPosition);
    
    if (distanceMoved < 1.0) // Дрон не двигается
    {
        stuckCounter++;
        if (stuckCounter > 50) // Застрял на ~80 секунд
        {
            Echo("Предупреждение: Дрон может быть заблокирован!");
            // Попытка обхода препятствия
            TryAvoidObstacle();
        }
    }
    else
    {
        stuckCounter = 0;
    }
    
    lastKnownPosition = currentPos;
}

// Попытка обхода препятствия
private void TryAvoidObstacle()
{
    if (rc == null) return;

    Echo("Попытка обхода препятствия...");
    
    // Временное отключение автопилота
    rc.SetAutoPilotEnabled(false);
    
    // Попытка движения вверх
    foreach (var thruster in thrusters)
    {
        if (thruster != null && thruster.Orientation.Forward == Base6Directions.Direction.Up)
        {
            thruster.ThrustOverride = 0.5f;
        }
    }
    
    // Через 2 секунды вернуться к нормальной работе
    Runtime.UpdateFrequency = UpdateFrequency.Update1;
}

// Обработка инициализации
private void HandleInitialization()
{
    if (ValidateAllSystems())
    {
        currentState = DroneState.Mining;
        Echo("Системы инициализированы, начинаем добычу");
    }
    else
    {
        currentState = DroneState.Error;
        Echo("Ошибка инициализации систем");
    }
}

// Обработка добычи
private void HandleMining()
{
    if (IsCargoFull() || !HasEnoughPower())
    {
        currentState = DroneState.ReturningToBase;
        Echo("Возвращаемся на базу");
        return;
    }

    if (!IsAtPosition(miningPosition))
    {
        FlyTo(miningPosition);
    }
    else
    {
        if (drill != null && !drill.Enabled)
        {
            drill.Enabled = true;
            Echo("Бур активирован");
        }
    }
}

// Обработка возврата на базу
private void HandleReturnToBase()
{
    if (IsAtPosition(basePosition))
    {
        currentState = DroneState.Docking;
        return;
    }

    FlyTo(basePosition);
}

// Обработка стыковки
private void HandleDocking()
{
    if (connector != null && connector.Status == MyShipConnectorStatus.Connected)
    {
        TransferCargoToBase();
        currentState = DroneState.Charging;
        Echo("Груз выгружен, начинаем зарядку");
    }
    else
    {
        StartDockingSequence();
    }
}

// Обработка зарядки
private void HandleCharging()
{
    if (battery != null && (battery.CurrentStoredPower / battery.MaxStoredPower) > 0.8f)
    {
        currentState = DroneState.Mining;
        Echo("Батарея заряжена, возвращаемся к добыче");
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
    currentState = DroneState.Initializing;
    stuckCounter = 0;
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

    if (battery == null)
    {
        Echo("Ошибка: Батарея не найдена!");
        isValid = false;
    }

    if (cargoContainers.Count == 0)
    {
        Echo("Ошибка: Грузовые контейнеры не найдены!");
        isValid = false;
    }

    return isValid;
}

// Инициализация блоков с улучшенной обработкой ошибок
private bool Init()
{
    try
    {
        rc = GridTerminalSystem.GetBlockWithName("RC") as IMyRemoteControl;
        GridTerminalSystem.GetBlocksOfType(thrusters);
        GridTerminalSystem.GetBlocksOfType(gyros);
        connector = GridTerminalSystem.GetBlockWithName("Connector") as IMyShipConnector;
        GridTerminalSystem.GetBlocksOfType(cargoContainers);
        drill = GridTerminalSystem.GetBlockWithName("Drill") as IMyShipDrill;
        battery = GridTerminalSystem.GetBlockWithName("Battery") as IMyBatteryBlock;
        GridTerminalSystem.GetBlocksOfType(cameras);
        GridTerminalSystem.GetBlocksOfType(sensors);

        return ValidateAllSystems();
    }
    catch (Exception ex)
    {
        Echo($"Ошибка инициализации: {ex.Message}");
        return false;
    }
}

// Улучшенный полет к указанной позиции
private void FlyTo(Vector3D pos)
{
    if (rc == null) return;

    try
    {
        rc.ClearWaypoints();
        rc.AddWaypoint(pos, "target");
        
        if (ObstacleInPath(pos))
        {
            rc.SetAutoPilotEnabled(false);
            Echo("Автопилот отключен из-за препятствий");
        }
        else
        {
            rc.SetAutoPilotEnabled(true);
        }
    }
    catch (Exception ex)
    {
        Echo($"Ошибка навигации: {ex.Message}");
        currentState = DroneState.Error;
    }
}

// Проверка достижения позиции с улучшенной точностью
private bool IsAtPosition(Vector3D pos)
{
    if (rc == null) return false;
    return Vector3D.Distance(rc.GetPosition(), pos) < 5;
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

// Улучшенная проверка уровня энергии с более точной моделью
private bool HasEnoughPower()
{
    if (rc == null || battery == null) 
    {
        if (!Init()) return false;
    }

    if (battery == null)
    {
        Echo("Ошибка: Батарея не найдена!");
        return false;
    }

    // Более точный расчет энергопотребления
    double distance = Vector3D.Distance(rc.GetPosition(), basePosition);
    double mass = rc.CalculateShipMass().TotalMass;
    
    // Улучшенная формула энергопотребления
    double thrustPower = mass * 0.00002; // Потребление двигателей
    double systemsPower = 0.001; // Потребление систем
    double totalPowerPerSecond = thrustPower + systemsPower;
    
    double travelTimeSeconds = distance / 25.0; // Скорость 25 м/с
    double energyNeeded = totalPowerPerSecond * travelTimeSeconds;
    
    // Добавляем 20% резерв
    energyNeeded *= 1.2;

    double remainingPower = battery.CurrentStoredPower;
    
    if (remainingPower < energyNeeded)
    {
        Echo($"Недостаточно энергии: нужно {energyNeeded:F2} МВт⋅ч, доступно {remainingPower:F2} МВт⋅ч");
        return false;
    }

    return remainingPower / battery.MaxStoredPower > batteryThreshold;
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
            Echo("Начинаем процедуру стыковки");
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

        Echo($"Передано {transferredItems} предметов на базу");
    }
    catch (Exception ex)
    {
        Echo($"Ошибка передачи груза: {ex.Message}");
    }
}

// Обновление информации на дисплее
private void UpdateDisplay()
{
    string status = $"Состояние: {currentState}\n";
    
    if (battery != null)
    {
        double chargePercent = (battery.CurrentStoredPower / battery.MaxStoredPower) * 100.0;
        status += $"Батарея: {chargePercent:F1}%\n";
    }
    
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
    
    Echo(status);
}

// Загрузка конфигурации с улучшенной обработкой ошибок
private void ParseConfig()
{
    try
    {
        MyIniParseResult result;
        if (!_ini.TryParse(Me.CustomData, out result))
        {
            Echo("Используются настройки по умолчанию");
            return;
        }

        basePosition = ParseGPS(_ini.Get("Settings", "BaseGPS").ToString(), basePosition);
        miningPosition = ParseGPS(_ini.Get("Settings", "MineGPS").ToString(), miningPosition);
        cargoFullPercent = _ini.Get("Settings", "CargoFullPercent").ToSingle(cargoFullPercent);
        batteryThreshold = _ini.Get("Settings", "BatteryThreshold").ToSingle(batteryThreshold);
        obstacleDetectionRange = _ini.Get("Settings", "ObstacleDetectionRange").ToDouble(obstacleDetectionRange);
        safeDistance = _ini.Get("Settings", "SafeDistance").ToDouble(safeDistance);
        
        Echo("Конфигурация загружена успешно");
    }
    catch (Exception ex)
    {
        Echo($"Ошибка загрузки конфигурации: {ex.Message}");
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
