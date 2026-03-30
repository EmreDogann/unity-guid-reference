using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class GuidMappingsDebugWindow : EditorWindow
{
    private enum ItemKind
    {
        Record,
        TransformGuid,
        ComponentGuid,
        OrphanGuid
    }

    private struct TreeItemData
    {
        public ItemKind Kind;
        public string Label;
        public string Guid;
        public string GlobalObjectId;
    }

    private TreeView _treeView;
    private Label _countLabel;

    [MenuItem("Tools/Guid Referencing/Guid Mappings Viewer")]
    private static void ShowWindow()
    {
        GuidMappingsDebugWindow window = GetWindow<GuidMappingsDebugWindow>();
        window.titleContent = new GUIContent("Guid Mappings Viewer");
        window.Show();
    }

    private void CreateGUI()
    {
        VisualElement toolbar = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                paddingLeft = 4,
                paddingRight = 4,
                paddingTop = 4,
                paddingBottom = 4
            }
        };

        Button refreshButton = new Button(RebuildTree) { text = "Refresh" };
        Button rebuildButton = new Button(() =>
        {
            GuidMappings.RebuildGuidMappings();
            RebuildTree();
        }) { text = "Rebuild" };
        Button clearButton = new Button(() =>
        {
            GuidMappings.Instance.Clear();
            RebuildTree();
        }) { text = "Clear" };
        toolbar.Add(refreshButton);
        toolbar.Add(rebuildButton);
        toolbar.Add(clearButton);

        _countLabel = new Label
        {
            style =
            {
                unityTextAlign = TextAnchor.MiddleLeft,
                marginLeft = 8
            }
        };
        toolbar.Add(_countLabel);

        rootVisualElement.Add(toolbar);

        _treeView = new TreeView
        {
            viewDataKey = "guid-mappings-tree",
            virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
            selectionType = SelectionType.None,
            makeItem = MakeItem,
            bindItem = BindItem,
            style = { flexGrow = 1 }
        };

        rootVisualElement.Add(_treeView);

        GuidComponent.OnCacheGuid += OnMappingsChanged;
        GuidComponent.OnGuidRemoved += OnMappingsChanged;
        GuidComponent.OnCacheOrphan += OnMappingsChanged;
        GuidComponent.OnOrphanRemoved += OnMappingsChanged;
        GuidComponent.OnGuidComponentDestroying += OnGuidComponentDestroying;

        PrefabUtility.prefabInstanceUpdated += PrefabInstanceUpdated;

        RebuildTree();
    }

    private void OnDestroy()
    {
        GuidComponent.OnCacheGuid -= OnMappingsChanged;
        GuidComponent.OnGuidRemoved -= OnMappingsChanged;
        GuidComponent.OnCacheOrphan -= OnMappingsChanged;
        GuidComponent.OnOrphanRemoved -= OnMappingsChanged;
        GuidComponent.OnGuidComponentDestroying -= OnGuidComponentDestroying;

        PrefabUtility.prefabInstanceUpdated -= PrefabInstanceUpdated;
    }

    private void OnMappingsChanged(ComponentGuid _)
    {
        EditorApplication.delayCall += RebuildTree;
    }

    private void OnGuidComponentDestroying(GuidComponent _)
    {
        EditorApplication.delayCall += RebuildTree;
    }

    private void PrefabInstanceUpdated(GameObject _)
    {
        EditorApplication.delayCall += RebuildTree;
    }

    private static VisualElement MakeItem()
    {
        VisualElement container = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                paddingTop = 2,
                paddingBottom = 2
            }
        };

        Label label = new Label { name = "item-label" };
        container.Add(label);

        TextField guidField = new TextField
        {
            name = "guid-field",
            isReadOnly = true,
            style =
            {
                flexGrow = 1,
                marginLeft = 4
            }
        };
        container.Add(guidField);

        return container;
    }

    private void BindItem(VisualElement element, int index)
    {
        TreeItemData item = _treeView.GetItemDataForIndex<TreeItemData>(index);
        Label label = element.Q<Label>("item-label");
        TextField guidField = element.Q<TextField>("guid-field");

        switch (item.Kind)
        {
            case ItemKind.Record:
                label.text = item.Label;
                guidField.style.display = DisplayStyle.None;
                break;
            case ItemKind.TransformGuid:
                label.text = "Transform";
                guidField.value = item.Guid;
                guidField.style.display = DisplayStyle.Flex;
                break;
            case ItemKind.ComponentGuid:
                label.text = item.Label;
                guidField.value = item.Guid;
                guidField.style.display = DisplayStyle.Flex;
                break;
            case ItemKind.OrphanGuid:
                label.text = $"[Orphan] {item.Label}";
                guidField.value = item.Guid;
                guidField.style.display = DisplayStyle.Flex;
                break;
        }
    }

    private void RebuildTree()
    {
        var rootItems = new List<TreeViewItemData<TreeItemData>>();

        foreach (var kvp in GuidMappings.Instance.Records)
        {
            string transformKey = kvp.Key;
            GuidMappings.GuidRecord record = kvp.Value;
            var children = new List<TreeViewItemData<TreeItemData>>();

            if (record.transformGuid != null)
            {
                children.Add(new TreeViewItemData<TreeItemData>(
                    (transformKey + ":transform").GetHashCode(),
                    new TreeItemData
                    {
                        Kind = ItemKind.TransformGuid,
                        Guid = record.transformGuid.guid.ToString()
                    }));
            }

            foreach (GuidMappings.GuidItem guidItem in record.assignedGuids)
            {
                children.Add(new TreeViewItemData<TreeItemData>(
                    guidItem.globalObjectID.GetHashCode(),
                    new TreeItemData
                    {
                        Kind = ItemKind.ComponentGuid,
                        Label = ResolveComponentTypeName(guidItem.globalObjectID),
                        Guid = guidItem.guid.ToString(),
                        GlobalObjectId = guidItem.globalObjectID
                    }));
            }

            foreach (GuidMappings.OrphanGuidItem orphan in record.orphanedGuids)
            {
                string typeName = ResolveTypeShortName(orphan.ownerTypeReference);
                children.Add(new TreeViewItemData<TreeItemData>(
                    (transformKey + ":orphan:" + orphan.guid).GetHashCode(),
                    new TreeItemData
                    {
                        Kind = ItemKind.OrphanGuid,
                        Label = typeName,
                        Guid = orphan.guid.ToString()
                    }));
            }

            string gameObjectName = ResolveObjectName(transformKey);
            rootItems.Add(new TreeViewItemData<TreeItemData>(
                transformKey.GetHashCode(),
                new TreeItemData
                {
                    Kind = ItemKind.Record,
                    Label = gameObjectName,
                    GlobalObjectId = transformKey
                }, children));
        }

        _treeView.SetRootItems(rootItems);
        _treeView.Rebuild();
        _countLabel.text = $"Records: {rootItems.Count}";
    }

    private static string ResolveObjectName(string globalObjectIdString)
    {
        if (string.IsNullOrEmpty(globalObjectIdString))
        {
            return "(empty)";
        }

        if (!GlobalObjectId.TryParse(globalObjectIdString, out GlobalObjectId globalObjectId))
        {
            return "(invalid ID)";
        }

        Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
        return obj != null ? obj.name : "(unresolved)";
    }

    private static string ResolveTypeShortName(string assemblyQualifiedName)
    {
        if (string.IsNullOrEmpty(assemblyQualifiedName))
        {
            return "(unknown type)";
        }

        Type type = Type.GetType(assemblyQualifiedName);
        return type != null ? type.Name : assemblyQualifiedName;
    }

    private static string ResolveComponentTypeName(string globalObjectIdString)
    {
        if (string.IsNullOrEmpty(globalObjectIdString))
        {
            return "(empty)";
        }

        if (!GlobalObjectId.TryParse(globalObjectIdString, out GlobalObjectId globalObjectId))
        {
            return "(invalid ID)";
        }

        Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId);
        return obj != null ? obj.GetType().Name : "(unresolved)";
    }
}