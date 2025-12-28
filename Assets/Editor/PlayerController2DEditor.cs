using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;

[CustomEditor(typeof(PlayerController2D))]
public class PlayerController2DEditor : Editor
{
    SerializedProperty elementNowUIProp;
    SerializedProperty elementNext1UIProp;
    SerializedProperty elementNext2UIProp;

    SerializedProperty elementNowUIListProp;
    SerializedProperty elementNext1UIListProp;
    SerializedProperty elementNext2UIListProp;

    ReorderableList nowList;
    ReorderableList next1List;
    ReorderableList next2List;

    void OnEnable()
    {
        elementNowUIProp = serializedObject.FindProperty("elementNowUI");
        elementNext1UIProp = serializedObject.FindProperty("elementNext1UI");
        elementNext2UIProp = serializedObject.FindProperty("elementNext2UI");

        elementNowUIListProp = serializedObject.FindProperty("elementNowUIList");
        elementNext1UIListProp = serializedObject.FindProperty("elementNext1UIList");
        elementNext2UIListProp = serializedObject.FindProperty("elementNext2UIList");
        // elementCycleModeProp = serializedObject.FindProperty("elementCycleMode");
        // serializedElementQueueProp = serializedObject.FindProperty("serializedElementQueue");
        // elementSpritesProp = serializedObject.FindProperty("elementSprites");

        nowList = MakeReorderableList(elementNowUIListProp, "Element Now UI List");
        next1List = MakeReorderableList(elementNext1UIListProp, "Element Next1 UI List");
        next2List = MakeReorderableList(elementNext2UIListProp, "Element Next2 UI List");
        // Element queue UI removed (reverted)
        
        // elementSprites list removed (reverted)
    }

    // elementQueueList removed (reverted)

    ReorderableList MakeReorderableList(SerializedProperty prop, string header)
    {
        var list = new ReorderableList(serializedObject, prop, true, true, true, true);
        list.drawHeaderCallback = (Rect rect) =>
        {
            EditorGUI.LabelField(rect, header);
        };
        list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
        {
            var element = prop.GetArrayElementAtIndex(index);
            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.ObjectField(new Rect(rect.x, rect.y, rect.width, rect.height), element, GUIContent.none);
        };
        list.elementHeight = EditorGUIUtility.singleLineHeight + 6;
        return list;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // Draw default inspector excluding the lists so we can show them in custom places
        DrawPropertiesExcluding(serializedObject, "elementNowUIList", "elementNext1UIList", "elementNext2UIList");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Element UI Lists", EditorStyles.boldLabel);

        // Show single-field fallback and lists
        EditorGUILayout.PropertyField(elementNowUIProp, new GUIContent("Element Now UI (single)"));
        nowList.DoLayoutList();

        EditorGUILayout.PropertyField(elementNext1UIProp, new GUIContent("Element Next1 UI (single)"));
        next1List.DoLayoutList();

        EditorGUILayout.PropertyField(elementNext2UIProp, new GUIContent("Element Next2 UI (single)"));
        next2List.DoLayoutList();

        serializedObject.ApplyModifiedProperties();
    }
}