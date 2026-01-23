using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using PopupWindow = UnityEditor.PopupWindow;

// From: https://discussions.unity.com/t/serializable-system-type-get-it-while-its-hot/508053/13
#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(SerializableType), true)]
internal class SerializableTypeDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        // Type targetObjectType = DetermineTargetType(fieldInfo.FieldType);
        SerializableTypeField field = new SerializableTypeField(null, property.displayName);
        SerializedProperty targetProperty = property.FindPropertyRelative("data");
        field.TrackPropertyValue(targetProperty, field.UpdateDisplay);
        field.BindProperty(targetProperty);
        field.UpdateDisplay(targetProperty);
        return field;
    }

    private static Type GetElementType(Type t)
    {
        if (t.IsGenericType)
        {
            return t.GetGenericArguments()[0];
        }

        return t;
    }

    private static Type DetermineTargetType(Type t)
    {
        if (typeof(IEnumerable).IsAssignableFrom(t) && t.IsGenericType)
        {
            return GetElementType(t.GetGenericArguments()[0]);
        }

        if (t.IsArray)
        {
            return GetElementType(t.GetElementType());
        }

        return GetElementType(t);
    }
}
public static class SerializableTypeUtility
{
    // Type serialization from https://discussions.unity.com/t/508053/10
    public static Type Read(BinaryReader aReader)
    {
        byte paramCount = aReader.ReadByte();
        if (paramCount == 0xFF)
        {
            return null;
        }

        string typeName = aReader.ReadString();
        Type type = Type.GetType(typeName);
        if (type == null)
        {
            throw new Exception("Can't find type; '" + typeName + "'");
        }

        if (type.IsGenericTypeDefinition && paramCount > 0)
        {
            var p = new Type[paramCount];
            for (int i = 0; i < paramCount; i++)
            {
                Type typeArgument = Read(aReader);
                if (typeArgument == null)
                {
                    return type;
                }

                p[i] = typeArgument;
            }

            type = type.MakeGenericType(p);
        }

        return type;
    }

    public static Type ReadFromString(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        int n = (int)((long)text.Length * 3 / 4);
        byte[] tmp = ArrayPool<byte>.Shared.Rent(n);
        try
        {
            if (!Convert.TryFromBase64String(text, tmp, out int nActual))
            {
                return null;
            }

            using (MemoryStream stream = new MemoryStream(tmp, 0, nActual))
            using (BinaryReader r = new BinaryReader(stream))
            {
                return Read(r);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tmp);
        }
    }

    public static string WriteToString(Type type)
    {
        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter w = new BinaryWriter(stream))
        {
            Write(w, type);
            return Convert.ToBase64String(stream.ToArray());
        }
    }

    public static void Write(BinaryWriter aWriter, Type aType)
    {
        if (aType == null || aType.IsGenericParameter)
        {
            aWriter.Write((byte)0xFF);
            return;
        }

        if (aType.IsGenericType)
        {
            Type t = aType.GetGenericTypeDefinition();
            var p = aType.GetGenericArguments();
            aWriter.Write((byte)p.Length);
            aWriter.Write(t.AssemblyQualifiedName);
            for (int i = 0; i < p.Length; i++)
            {
                Write(aWriter, p[i]);
            }

            return;
        }

        aWriter.Write((byte)0);
        aWriter.Write(aType.AssemblyQualifiedName);
    }
}

internal static class SerializableTypeDataUtility
{
    private static readonly Dictionary<Type, List<Type>> s_map = new Dictionary<Type, List<Type>>();
    private static readonly Dictionary<Type, List<Type>> s_mapWithNull = new Dictionary<Type, List<Type>>();

    internal static List<Type> GetFilteredTypesAndNull(Type type)
    {
        if (!s_mapWithNull.TryGetValue(type, out var list))
        {
            s_mapWithNull.Add(type, list = new List<Type> { null });
            list.AddRange(GetFilteredTypes(type));
        }

        return list;
    }

    internal static List<Type> GetFilteredTypes(Type type)
    {
        if (!s_map.TryGetValue(type, out var list))
        {
            s_map.Add(type, list = GetFilteredTypesInternal(type));
        }

        return list;
    }

    private static List<Type> GetFilteredTypesInternal(Type type)
    {
        var types = new HashSet<Type>(TypeCache.GetTypesDerivedFrom(type));
        types.Add(type);
        return types.OrderBy(static s => s.FullName, StringComparer.InvariantCulture).ToList();
    }
}

internal class SerializableTypeField : BaseField<string>
{
    private Type _filterType;
    private readonly VisualElement _content;
    private readonly Image _image;
    private readonly Label _label;
    private readonly VisualElement _warningBox;
    private readonly Button _selectButton;
    private readonly TypeSelectorPopupWindowContent _typeSelectorPopupWindowContent;
    private readonly List<Type> _filteredTypes;

    public SerializableTypeField(Type filterType, string label) : this(filterType, label, new VisualElement()) {}

    public SerializableTypeField(Type filterType, string label, VisualElement visualInput) : base(label, visualInput)
    {
        _filterType = filterType;
        AddToClassList(alignedFieldUssClassName);

        _content = visualInput;
        _content.AddToClassList(ObjectField.inputUssClassName);
        _content.AddToClassList(disabledUssClassName);
        _content.style.flexDirection = FlexDirection.Row;

        _image = new Image();
        _image.AddToClassList("unity-object-field-display__icon");

        _content.Add(_image);
        _content.Add(_label = new Label());
        _content.Add(_warningBox = new VisualElement
        {
            style =
            {
                width = 16,
                height = 16,
                backgroundImage = new StyleBackground(EditorGUIUtility.IconContent("warning").image as Texture2D),
                backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center),
                backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center),
                backgroundRepeat = new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat),
                backgroundSize = new BackgroundSize(BackgroundSizeType.Contain)
            },
            tooltip =
                $"The currently assigned type is not compatible with the property's type restriction ({_filterType}). The type will be considered null when read."
        });

        _warningBox.style.display = DisplayStyle.None;

        // _content.Add(_selectButton = new Button(OpenSelector)
        // {
        //     style =
        //     {
        //         marginBottom = 0,
        //         marginLeft = 0,
        //         marginRight = 0,
        //         marginTop = 0,
        //         borderBottomWidth = 0,
        //         borderLeftWidth = 0,
        //         borderRightWidth = 0,
        //         borderTopWidth = 0,
        //         paddingBottom = 0,
        //         paddingLeft = 0,
        //         paddingRight = 0,
        //         paddingTop = 0
        //     }
        // });

        _label.AddToClassList("unity-object-field-display__label");
        // _selectButton.AddToClassList(ObjectField.selectorUssClassName);
        // _typeSelectorPopupWindowContent = new TypeSelectorPopupWindowContent(this);

        if (filterType != null)
        {
            _filteredTypes = SerializableTypeDataUtility.GetFilteredTypesAndNull(filterType);
        }
    }

    protected override void UpdateMixedValueContent()
    {
        UpdateDisplay("\u2014");
    }

    internal void UpdateDisplay(SerializedProperty serializedProperty)
    {
        byte[] buffer = new byte[serializedProperty.arraySize];
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)serializedProperty.GetArrayElementAtIndex(i).intValue;
        }

        using (MemoryStream stream = new MemoryStream(buffer))
        using (BinaryReader reader = new BinaryReader(stream))
        {
            _filterType = SerializableType.Read(reader);
            UpdateDisplay(_filterType);
        }
    }

    private void UpdateDisplay(Type type)
    {
        if (type != null)
        {
            if (_filterType != null)
            {
                _warningBox.style.display = _filterType.IsAssignableFrom(type) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            UpdateDisplay(GetFormattedType(type));
        }
        else
        {
            _warningBox.style.display = DisplayStyle.None;
            UpdateDisplay("Not Set");
        }
    }

    private void UpdateDisplay(string text)
    {
        _image.image = EditorGUIUtility.ObjectContent(null, _filterType).image;
        _label.text = text;
    }

    internal static string GetFormattedType(Type t)
    {
        return t != null ? $"{t.Name} ({t.FullName})" : "None";
    }

    public List<Type> GetFilteredTypes()
    {
        return _filteredTypes;
    }

    private void OpenSelector()
    {
        PopupWindow.Show(worldBound, _typeSelectorPopupWindowContent);
    }

    private class TypeSelectorPopupWindowContent : PopupWindowContent
    {
        private const int itemHeight = 18;
        private readonly SerializableTypeField _field;
        private readonly Toolbar _toolbar;
        private readonly ToolbarSearchField _search;
        private readonly ListView _listView;
        private List<Type> _types;
        private bool _pauseSelectionCheck;

        internal TypeSelectorPopupWindowContent(SerializableTypeField field)
        {
            _field = field;
            _listView = new ListView
            {
                reorderable = false,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.All,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                fixedItemHeight = itemHeight
            };
            _listView.makeItem = () => new Label();
            _listView.bindItem = (v, i) =>
            {
                Type t = ((IReadOnlyList<Type>)_listView.itemsSource)[i];
                ((Label)v).text = GetFormattedType(t);
            };
            _listView.selectionChanged += s => { SetFromSelection(s); };
            _listView.itemsChosen += s =>
            {
                SetFromSelection(s);
                editorWindow.Close();
            };
            _toolbar = new Toolbar();
            _search = new ToolbarSearchField();
            DelayedSearchController searchState = new DelayedSearchController(_search);
            searchState.SearchTextChanged += s => { SetFilter(s); };
            _toolbar.Add(_search);
        }

        private void SetFromSelection(IEnumerable<object> selection)
        {
            if (_pauseSelectionCheck)
            {
                return;
            }

            Type type = selection.FirstOrDefault() as Type;
            _field.value = SerializableTypeUtility.WriteToString(type);
        }

        private void SetList(List<Type> types)
        {
            _listView.itemsSource = types;

            int index = types.IndexOf(SerializableTypeUtility.ReadFromString(_field.value));
            if (index >= 0)
            {
                _listView.SetSelection(index);
            }
            else
            {
                _listView.ClearSelection();
            }
        }

        private void SetFilter(string filter)
        {
            _pauseSelectionCheck = true;
            if (string.IsNullOrEmpty(filter))
            {
                SetList(_types);
            }
            else
            {
                var filtered = new List<Type>();
                foreach (Type type in _types)
                {
                    if (type.FullName.Contains(filter, StringComparison.InvariantCultureIgnoreCase))
                    {
                        filtered.Add(type);
                    }
                }

                SetList(filtered);
            }

            _pauseSelectionCheck = false;
        }

        public override Vector2 GetWindowSize()
        {
            float width = Math.Max(200, _field.resolvedStyle.width);
            return new Vector2(width, 500);
        }

        public override void OnOpen()
        {
            editorWindow.rootVisualElement.Clear();
            editorWindow.rootVisualElement.Add(_toolbar);
            editorWindow.rootVisualElement.Add(_listView);
            _types = _field.GetFilteredTypes();
            SetList(_types);
            editorWindow.rootVisualElement.RegisterCallback<NavigationCancelEvent>(HandleNavigationCancelEvent);
            editorWindow.rootVisualElement.schedule.Execute(() => { _search.Focus(); });
        }

        public override void OnClose()
        {
            editorWindow.rootVisualElement.UnregisterCallback<NavigationCancelEvent>(HandleNavigationCancelEvent);
        }

        private void HandleNavigationCancelEvent(NavigationCancelEvent evt)
        {
            editorWindow.Close();
        }

        private class DelayedSearchController : IDisposable
        {
            private const int DelayMs = 300;

            public event Action<string> SearchTextChanged;

            private readonly VisualElement _search;
            private readonly int _delayMs;
            private string _activeText;
            private readonly IVisualElementScheduledItem _scheduledItem;
            private bool _disposed;

            public DelayedSearchController(VisualElement search, int delayMs = DelayMs)
            {
                _search = search;
                _delayMs = delayMs;
                _activeText = "";
                _scheduledItem = null;
                search.RegisterCallback<ChangeEvent<string>>(OnSearchChanged);
                search.RegisterCallback<NavigationSubmitEvent>(OnSearchEnter);
                _scheduledItem = search.schedule.Execute(UpdateItem);
                _scheduledItem.Pause();
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _search.UnregisterCallback<ChangeEvent<string>>(OnSearchChanged);
                _search.UnregisterCallback<NavigationSubmitEvent>(OnSearchEnter);
                _scheduledItem.Pause();
            }

            private void OnSearchEnter(NavigationSubmitEvent evt)
            {
                if (_disposed)
                {
                    return;
                }

                _scheduledItem?.ExecuteLater(0);
            }

            private void OnSearchChanged(ChangeEvent<string> evt)
            {
                if (_disposed)
                {
                    return;
                }

                _activeText = evt.newValue;
                if (string.IsNullOrEmpty(_activeText))
                {
                    _scheduledItem?.ExecuteLater(0);
                }
                else
                {
                    _scheduledItem?.ExecuteLater(_delayMs);
                }
            }

            private void UpdateItem()
            {
                SearchTextChanged?.Invoke(_activeText);
            }
        }
    }
}
#endif