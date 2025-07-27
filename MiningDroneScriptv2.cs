// Пример скрипта для программируемого блока в Space Engineers
// Этот скрипт обеспечивает простое автономное управление добывающим дроном
// Дрон добывает руду, пока его грузовой контейнер не заполнится
// Затем возвращается на базу, выгружает руду и продолжает добычу
// Примечание: Этот скрипт является упрощенной демонстрацией и может потребовать
// корректировок для конкретных кораблей или дополнительных проверок безопасности
// Базовое обнаружение препятствий выполняется с помощью переднего луча или датчика
// Камеры должны быть направлены вперед; дальность луча ограничена 50м,
// препятствия за пределами этого диапазона не будут обнаружены

// Настройки конфигурации
private MyIni _ini = new MyIni(); // INI парсер для чтения пользовательских настроек
private float cargoFullPercent = 0.9f; // Пороговое значение заполнения грузового отсека (90%)
private float batteryThreshold = 0.3f; // Пороговый уровень заряда батареи (30%)

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
private Vector3D basePosition = new Vector3D(0,0,0); // Координаты базы
private Vector3D miningPosition = new Vector3D(100,0,0); // Координаты точки добычи

// Главный метод программы
public void Main(string arg, UpdateType updateSource)
{
    if(rc == null) Init(); // Инициализация блоков при первом запуске

    if(arg == "dock") // Проверка команды стыковки
    {
        StartDockingSequence(); // Запуск процедуры стыковки
        return;
    }

    if(IsCargoFull() || !HasEnoughPower()) // Проверка заполнения груза или заряда батареи
    {
        FlyTo(basePosition); // Возврат на базу
    }
    else if(!IsAtPosition(miningPosition)) // Проверка нахождения в точке добычи
    {
        FlyTo(miningPosition); // Полет к точке добычи
    }
    else
    {
        if(!drill.Enabled) drill.Enabled = true; // Включение бура
    }
}

// Проверка препятствий на пути
private bool ObstacleInPath(Vector3D destination)
{
    double dist = Vector3D.Distance(rc.GetPosition(), destination); // Расчет расстояния до цели
    double range = Math.Min(dist, 50); // Ограничение дальности сканирования 50 метрами

    if(cameras.Count > 0) // Проверка наличия камер
    {
        var cam = cameras[0];
        if(cam.EnableRaycast && cam.CanScan(range)) // Проверка возможности сканирования
        {
            var info = cam.Raycast(range);
            if(!info.IsEmpty()) return true; // Обнаружено препятствие
        }
    }
    foreach(var s in sensors) // Проверка всех сенсоров
    {
        if(s.IsActive) return true; // Сенсор обнаружил препятствие
    }
    return false;
}

// Инициализация блоков
private void Init()
{
    rc = GridTerminalSystem.GetBlockWithName("RC") as IMyRemoteControl; // Получение блока управления
    GridTerminalSystem.GetBlocksOfType(thrusters); // Получение всех двигателей
    GridTerminalSystem.GetBlocksOfType(gyros); // Получение всех гироскопов
    connector = GridTerminalSystem.GetBlockWithName("Connector") as IMyShipConnector; // Получение коннектора
    if(connector == null)
    {
        Echo("Ошибка: Коннектор не найден!");
    }

    GridTerminalSystem.GetBlocksOfType(cargoContainers); // Получение грузовых контейнеров
    drill = GridTerminalSystem.GetBlockWithName("Drill") as IMyShipDrill; // Получение бура
    battery = GridTerminalSystem.GetBlockWithName("Battery") as IMyBatteryBlock; // Получение батареи

    // Получение блоков обнаружения препятствий
    GridTerminalSystem.GetBlocksOfType(cameras); // Получение камер
    GridTerminalSystem.GetBlocksOfType(sensors); // Получение сенсоров
}

// Полет к указанной позиции
private void FlyTo(Vector3D pos)
{
    rc.ClearWaypoints(); // Очистка старых путевых точек
    rc.AddWaypoint(pos, "target"); // Добавление новой цели
    if(ObstacleInPath(pos)) // Проверка препятствий
    {
        rc.SetAutoPilotEnabled(false); // Отключение автопилота при обнаружении препятствия
    }
    else
    {
        rc.SetAutoPilotEnabled(true); // Включение автопилота
    }
}

// Проверка достижения позиции
private bool IsAtPosition(Vector3D pos)
{
    return Vector3D.Distance(rc.GetPosition(), pos) < 5; // Проверка расстояния до цели (менее 5 метров)
}

// Проверка заполнения грузового отсека
private bool IsCargoFull()
{
    foreach(var container in cargoContainers) // Проверка каждого контейнера
    {
        var inv = container.GetInventory();
        if(inv.CurrentVolume >= inv.MaxVolume * cargoFullPercent) // Сравнение с пороговым значением
            return true;
    }
    return false;
}

// Проверка уровня энергии
private bool HasEnoughPower()
{
    // Если блоки не инициализированы, выполняем инициализацию
    if(rc == null || battery == null) Init();
    
    // Если батарея все еще не найдена, возвращаем false
    if(battery == null) {
        Echo("Ошибка: Батарея не найдена!");
        return false;
    }
    
    // Если удаленное управление не найдено, возвращаем false
    if(rc == null) {
        Echo("Ошибка: Блок удаленного управления не найден!");
        return false;
    }
    
    // Расчет необходимой энергии для возврата на базу
    double distance = Vector3D.Distance(rc.GetPosition(), basePosition); // Расстояние до базы
    double mass = rc.CalculateShipMass().TotalMass; // Масса корабля
    double avgPower = mass * 0.00001; // Расход энергии на килограмм при крейсерской скорости
    double travelTimeHours = (distance / 50.0) / 3600.0; // Время полета при скорости 50 м/с
    double energyNeeded = avgPower * travelTimeHours; // Необходимая энергия в МВтч

    double remainingPower = battery.CurrentStoredPower; // Оставшаяся энергия
    if (remainingPower < energyNeeded) // Проверка достаточности энергии
        return false;

    return remainingPower / battery.MaxStoredPower > batteryThreshold; // Проверка порогового значения
}

// Процедура стыковки
private void StartDockingSequence()
{
    if(rc == null) Init(); // Инициализация блоков при необходимости
    
    // Проверка наличия необходимых блоков
    if(rc == null) {
        Echo("Ошибка: Блок удаленного управления не найден!");
        return;
    }
    
    if(connector == null) {
        Echo("Ошибка: Коннектор не найден!");
        return;
    }
    
    rc.SetAutoPilotEnabled(false); // Отключение автопилота
    if(connector.Status != MyShipConnectorStatus.Connected) // Проверка состояния стыковки
    {
        rc.ClearWaypoints(); // Очистка путевых точек
        rc.AddWaypoint(basePosition, "base"); // Установка базы как цели
        rc.SetAutoPilotEnabled(true); // Включение автопилота
    }
    else
    {
        TransferCargoToBase(); // Передача груза на базу
    }
}

// Передача груза на базу
private void TransferCargoToBase()
{
    if(connector == null || connector.Status != MyShipConnectorStatus.Connected) // Проверка подключения
        return;
    var target = connector.GetInventory(); // Получение инвентаря базы
    foreach(var container in cargoContainers) // Перебор контейнеров
    {
        var inv = container.GetInventory();
        for(int i = inv.ItemCount - 1; i >= 0; i--) // Передача всех предметов
        {
            inv.TransferItemTo(target, i, null, true);
        }
    }
}

// Загрузка конфигурации
private void ParseConfig()
{
    MyIniParseResult result;
    if(!_ini.TryParse(Me.CustomData, out result)) // Попытка чтения конфигурации
        return;

    // Загрузка настроек из конфигурации
    basePosition = ParseGPS(_ini.Get("Settings", "BaseGPS").ToString(), basePosition);
    miningPosition = ParseGPS(_ini.Get("Settings", "MineGPS").ToString(), miningPosition);
    cargoFullPercent = _ini.Get("Settings", "CargoFullPercent").ToSingle(cargoFullPercent);
    batteryThreshold = _ini.Get("Settings", "BatteryThreshold").ToSingle(batteryThreshold);
}

// Парсинг GPS координат
private Vector3D ParseGPS(string value, Vector3D fallback)
{
    if(string.IsNullOrWhiteSpace(value)) return fallback; // Проверка пустого значения
    if(value.StartsWith("GPS:")) // Парсинг GPS формата
    {
        var parts = value.Split(':');
        if(parts.Length >= 5)
        {
            double x, y, z;
            if(double.TryParse(parts[2], out x) && double.TryParse(parts[3], out y) && double.TryParse(parts[4], out z))
                return new Vector3D(x, y, z);
        }
    }
    else // Парсинг простого формата координат
    {
        char[] sep = new char[] { ',', ';', ' ' };
        var parts = value.Split(sep, StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length == 3)
        {
            double x, y, z;
            if(double.TryParse(parts[0], out x) && double.TryParse(parts[1], out y) && double.TryParse(parts[2], out z))
                return new Vector3D(x, y, z);
        }
    }
    return fallback; // Возврат значения по умолчанию при ошибке парсинга
}
