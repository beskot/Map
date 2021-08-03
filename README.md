# AxamlMapControl #
![GitHub](https://img.shields.io/github/license/beskot/Map)
![GitHub repo size](https://img.shields.io/github/repo-size/beskot/Map)

A multi-platform data driven map control library for .Net.

<div align="center" >
    <img src="/AxamlMapControlSample/Assets/Example_1.png" width="400" height="400" style=“margin-right: 10px;”/>
</div>
   
### Usage ###

To use the library, add the following to your `App.xaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:local="using:AxamlMapControlSample"
             x:Class="AxamlMapControlSample.App">
    <Application.DataTemplates>
        <local:ViewLocator/>
    </Application.DataTemplates>

    <Application.Styles>        
        <FluentTheme Mode="Light"/>
        <StyleInclude Source="avares://AxamlMapControl/Themes/Default.axaml"/>
    </Application.Styles>
</Application>
```

Then, you can add map control to your application, as such:
```xml
<Grid>
    <Border BorderBrush="Transparent">
        <map:Map Background="#FFFEFEFE" MinZoomLevel="0" MaxZoomLevel="15" ZoomLevel="4" Center="15, 15">
            <map:Map.TileLayer>
                <map:MapTileLayer>
                    <map:MapTileLayer.TileSource>
                        <map:TileSource UriStringFormat="https://tile.osm.ch/switzerland/{z}/{x}/{y}.png"/>
                    </map:MapTileLayer.TileSource>
                </map:MapTileLayer>
            </map:Map.TileLayer>
            <map:MapGraticule/>
        </map:Map>
    </Border>
</Grid>
```

### License ###

AxamlMapControl is licensed under the [MIT License](LICENSE). Thanks!



