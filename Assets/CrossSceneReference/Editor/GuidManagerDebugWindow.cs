using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SceneHandling.Editor.UI.Controls
{
    public class GuidManagerDebugWindow : EditorWindow
    {
        private class GuidDebugInfo
        {
            public readonly Guid Guid;
            public readonly GuidComponent GuidComponent;

            public GuidDebugInfo(Guid guid, GuidManager.GuidInfo guidInfo)
            {
                Guid = guid;
                GuidComponent = guidInfo.GuidComponent;
            }

            public static List<GuidDebugInfo> DictionaryToGuidDebugInfos(
                Dictionary<Guid, GuidManager.GuidInfo> guidInfos)
            {
                var list = new List<GuidDebugInfo>();
                foreach (var entry in guidInfos)
                {
                    list.Add(new GuidDebugInfo(entry.Key, entry.Value));
                }

                return list;
            }
        }

        private List<GuidDebugInfo> _guidInfoSource;
        private int _itemID;
        private ListView _listView;

        [MenuItem("Tools/Guid Referencing/Guid Manager Viewer")]
        private static void ShowWindow()
        {
            GuidManagerDebugWindow window = GetWindow<GuidManagerDebugWindow>();
            window.titleContent = new GUIContent("Guid Manager Viewer");
            window.Show();
        }

        private void CreateGUI()
        {
            _guidInfoSource = GuidDebugInfo.DictionaryToGuidDebugInfos(GuidManager.GetGuidInfos);

            _listView = new ListView();
            _listView.itemsSource = _guidInfoSource;
            _listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            _listView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            _listView.selectionType = SelectionType.None;

            _itemID = 0;

            _listView.makeItem = () =>
            {
                VisualElement container = new VisualElement();
                Foldout foldout = new Foldout
                {
                    value = false,
                    viewDataKey = "Foldout" + _itemID,
                    style =
                    {
                        marginBottom = 4,
                        marginTop = 4
                    }
                };

                ObjectField componentField = new ObjectField("GuidComponent")
                    { allowSceneObjects = true };
                componentField.SetEnabled(false);
                foldout.Add(componentField);

                // TextField requestedGuidLabel = new TextField("Requested Guid")
                // {
                //     isReadOnly = true,
                //     selectAllOnFocus = false,
                //     selectAllOnMouseUp = false
                // };
                // foldout.Add(requestedGuidLabel);

                container.Add(foldout);
                _itemID++;

                return container;
            };

            _listView.bindItem = (element, i) =>
            {
                Foldout foldout = element[0] as Foldout;
                ObjectField componentField = foldout[0] as ObjectField;

                componentField.value = _guidInfoSource[i].GuidComponent;
                string val = componentField.value != null ? componentField.value.name : "";
                foldout.text = _guidInfoSource[i].Guid + $"\t({val})";
            };

            GuidManager.OnGuidAdded += () =>
            {
                _guidInfoSource = GuidDebugInfo.DictionaryToGuidDebugInfos(GuidManager.GetGuidInfos);
                _listView.itemsSource = _guidInfoSource;
                _listView.Rebuild();
            };

            GuidManager.OnGuidRemoved += () =>
            {
                _guidInfoSource = GuidDebugInfo.DictionaryToGuidDebugInfos(GuidManager.GetGuidInfos);
                _listView.itemsSource = _guidInfoSource;
                _listView.Rebuild();
            };

            rootVisualElement.Add(_listView);
        }
    }
}