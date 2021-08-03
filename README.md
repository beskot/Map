<style>
    img.picture {
        width: 45%;
        display:block;
        margin-left:auto;
        margin-right:auto;
        border-radius: 255px 15px 225px 15px/15px 225px 15px 255px;
        padding:1em 0em 0em 0em;
        border:dashed 2px rgb(128, 128, 128);
        box-shadow: 0 14px 28px rgba(0,0,0,0.25), 0 10px 10px rgba(0,0,0,0.22)
    }
    p { color: #505050; }
</style>

# AxamlMapControl #

A multi-platform data driven map control library for .Net.
<img class="picture" src="/AxamlMapControlSample/Assets/Example_1.png"/>

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



