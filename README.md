# AxamlMapControl

Контрол карты для Avalonia: растровые тайлы, географические фигуры, маркеры и координатная сетка.

Документация относится к ветке `fix/axamlmapcontrol-stability`. Текущие проекты используют .NET 10 и Avalonia 12.1.4.

## Подключение

### 1. Добавьте ссылку на проект

Получите исходники:

```sh
git clone --branch fix/axamlmapcontrol-stability https://github.com/beskot/Map.git
```

Из каталога своего приложения добавьте ссылку, исправив пути под вашу структуру:

```sh
dotnet add YourApp.csproj reference ../Map/AxamlMapControl/AxamlMapControl.csproj
```

Приложение должно поддерживать `net10.0`. Согласуйте версии пакетов Avalonia с библиотекой; в приложении-примере используется 12.1.4. Эта инструкция использует исходники, а не предполагаемый NuGet-пакет.

### 2. Подключите стили

В существующий раздел `Application.Styles` файла `App.axaml` добавьте:

```xml
<StyleInclude Source="avares://AxamlMapControl/Themes/Default.axaml"/>
```

Например, с FluentTheme:

```xml
<Application.Styles>
    <FluentTheme/>
    <StyleInclude Source="avares://AxamlMapControl/Themes/Default.axaml"/>
</Application.Styles>
```

Не добавляйте второй `FluentTheme`, если тема уже подключена. Для него нужен пакет `Avalonia.Themes.Fluent` совместимой версии. Стили библиотеки включают оформление гратикулы и контейнеров элементов карты.

### 3. Добавьте карту и источник тайлов

Содержимое окна или представления:

```xml
<Grid xmlns="https://github.com/avaloniaui"
      xmlns:map="using:AxamlMapControl.Source">
    <map:Map Background="#F5F5F5"
             Center="46.8, 8.2"
             MinZoomLevel="0"
             MaxZoomLevel="15"
             ZoomLevel="4"
             IsZoomAnimationEnabled="True"
             ZoomAnimationDuration="0:0:0.2">
        <map:Map.TileLayer>
            <map:MapTileLayer>
                <map:MapTileLayer.TileSource>
                    <map:TileSource UriStringFormat="https://tiles.example/{z}/{x}/{y}.png"/>
                </map:MapTileLayer.TileSource>
            </map:MapTileLayer>
        </map:Map.TileLayer>
        <map:MapGraticule/>
    </map:Map>
</Grid>
```

`tiles.example` — заглушка, не рабочий сервер. Замените адрес своим источником тайлов. Без настроенного источника подложка не загрузится.

- `Center` задаётся как `широта, долгота`.
- В URL заменяются `{z}`, `{x}`, `{y}`; слой рассчитан на тайлы 256 × 256.
- `MinZoomLevel` и `MaxZoomLevel` должны соответствовать источнику.
- Перемещение — левой кнопкой мыши, зум — колесом относительно указателя.
- Анимация зума по умолчанию выключена; пример явно включает её.
- По вертикали перемещение ограничено границами карты и окна на всех масштабах.

## Фигуры и маркеры

Для фигур добавьте пространство имён:

```xml
xmlns:shape="using:AxamlMapControl.Source.Shapes"
```

Внутри `map:Map`, после настройки тайлов, можно разместить слой:

```xml
<map:MapLocationLayer>
    <shape:MapLine StartPoint="46.8, 8.2"
                   EndPoint="47.0, 8.5"
                   Stroke="Crimson"
                   StrokeThickness="3"/>
    <Border map:MapLocationLayer.Location="46.8, 8.2"
            HorizontalAlignment="Center"
            VerticalAlignment="Center"
            Background="White"
            Padding="4">
        <TextBlock Text="Маркер"/>
    </Border>
</map:MapLocationLayer>
```

Для коллекции объектов используйте `MapItemsControl`. Здесь `Objects` — коллекция в `DataContext`, а `Position` — свойство её элемента типа `AxamlMapControl.Source.Proj.Location`:

```xml
<map:MapItemsControl ItemsSource="{Binding Objects}" SelectionMode="Multiple">
    <map:MapItemsControl.Styles>
        <Style Selector="map|MapItem">
            <Setter Property="Location" Value="{Binding Position}"/>
            <Setter Property="HorizontalAlignment" Value="Center"/>
            <Setter Property="VerticalAlignment" Value="Center"/>
            <Setter Property="Template">
                <ControlTemplate>
                    <Ellipse Width="14" Height="14"
                             Fill="Crimson" Stroke="White" StrokeThickness="2"/>
                </ControlTemplate>
            </Setter>
        </Style>
    </map:MapItemsControl.Styles>
</map:MapItemsControl>
```

Добавляйте этот блок непосредственно в `map:Map`: `MapItemsControl` уже использует `MapLocationLayer` как панель элементов. Примеры рассчитаны на обычные reflection bindings, как в sample; при включённых compiled bindings укажите соответствующий `x:DataType` или явно используйте reflection binding.

## Что доступно

| Компонент | Назначение |
| --- | --- |
| `Map` | Центр, масштаб, перемещение мышью, опциональная анимация зума |
| `MapTileLayer`, `TileSource` | Растровые тайлы из источника с шаблоном URL |
| `ITileSource`, `ITileImageLoader` | Подключение собственного источника или загрузчика |
| `TileImageLoader` | Асинхронная HTTP-загрузка тайлов |
| `TileImageCache` | Кэш тайлов в памяти, используемый слоем по умолчанию |
| `MapGraticule` | Сетка широты и долготы, векторная анимация и парные подписи с проверкой наложения |
| `MapLocationLayer` | Размещение контролов по географическим координатам |
| `MapItemsControl`, `MapItem` | Привязка коллекции, шаблоны маркеров, одиночный и множественный выбор |
| `MapLine`, `MapPolyline`, `MapPolygon`, `MapPath` | Линии, ломаные, полигоны и геометрия |
| `ViewTransform` | Преобразования координат, масштаб и ограничения перемещения |
| `MapBaseElement`, `MapBaseLayer` | Базовые классы для собственных элементов и слоёв |

`TileImageCache` находится в пространстве имён `AxamlMapControl.Source.Cache`. По умолчанию он хранит до 64 МиБ закодированных изображений на экземпляр; срок записи — 10 минут. Это не ограничение общей памяти приложения.

### Важные ограничения

- У `MapItemsControl` нет собственной географической виртуализации или кэша отрисовки: панель обрабатывает все дочерние элементы, в том числе вне экрана.
- Кэш тайлов в памяти не является постоянным офлайн-хранилищем.
- Библиотека не предоставляет тайловый сервер и не добавляет атрибуцию провайдера автоматически. Соблюдайте условия использования и кэширования выбранного источника.

## Запуск примера

Из корня репозитория, с установленным .NET 10 SDK:

```sh
dotnet build Map.sln --configuration Release
dotnet run --project AxamlMapControlSample/AxamlMapControlSample.csproj --configuration Release
```

Для восстановления зависимостей и загрузки некэшированных тайлов нужен доступ к сети. В sample настроен региональный источник тайлов Швейцарии; подберите центр и масштаб в пределах его покрытия либо замените источник.

Полные примеры: [App.axaml](AxamlMapControlSample/App.axaml) и [MainWindow.axaml](AxamlMapControlSample/Views/MainWindow.axaml). Наличие примера не означает, что текущий коммит прошёл визуальные тесты.

## Лицензия

[MIT](LICENSE). У картографических данных и тайлов — отдельные условия их поставщиков.
