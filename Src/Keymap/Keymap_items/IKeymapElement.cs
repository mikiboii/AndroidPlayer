namespace Androidplayer.Src.Keymap.Keymap_items;

public interface IKeymapElement
{
    string Name { get; set; }
    string KeyName { get; set; }
        
    object GetJsonData();
    void SetJsonData(KeymapElement data); 
}