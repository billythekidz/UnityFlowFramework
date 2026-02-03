using System;
using System.Collections.Generic;

/// <summary>
/// Chứa các lớp data model được sử dụng để serialize/deserialize
/// thông tin cấu trúc cây cho hệ thống Object Binding.
/// </summary>
namespace UIBind.DataModels
{
    [Serializable]
    public class HierarchyNode
    {
        public string name;
        public string[] components;
        public List<HierarchyNode> children = new List<HierarchyNode>();
    }

    [Serializable]
    public class CachedObject
    {
        public string className; // Ví dụ: "MyPanelView"
        public HierarchyNode hierarchy;
    }

    [Serializable]
    public class HierarchyCache
    {
        public List<CachedObject> objects = new List<CachedObject>();
    }
}
