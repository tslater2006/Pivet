using System;
using System.Collections;
using System.Collections.Generic;
using Terminal.Gui;

namespace Pivet.GUI
{
    /// <summary>
    /// Generic wrapper class to make any IList compatible with Terminal.Gui ListView
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ListWrapper : IListDataSource
    {
        private readonly IList _list;

        public ListWrapper(IList list)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
        }

        public int Count => _list.Count;

        public int Length => _list.Count;

        public bool IsMarked(int item)
        {
            return false; // No marking support for now
        }

        public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0)
        {
            if (item < 0 || item >= _list.Count)
                return;

            var itemText = _list[item]?.ToString() ?? "";
            
            // Ensure we don't exceed the width
            if (itemText.Length > width)
                itemText = itemText.Substring(0, width);

            // Pad the text to fill the width
            itemText = itemText.PadRight(width);

            driver.AddStr(itemText);
        }

        public void SetMark(int item, bool value)
        {
            // No marking support implemented
        }

        public IList ToList()
        {
            return _list;
        }
    }

    /// <summary>
    /// Non-generic ListWrapper for backwards compatibility
    /// </summary>
    public class ListWrapper<T> : IListDataSource where T : class
    {
        private readonly IList<T> _list;

        public ListWrapper(IList<T> list)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
        }

        public int Count => _list.Count;

        public int Length => _list.Count;

        public bool IsMarked(int item)
        {
            return false;
        }

        public void Render(ListView container, ConsoleDriver driver, bool selected, int item, int col, int line, int width, int start = 0)
        {
            if (item < 0 || item >= _list.Count)
                return;

            var itemText = _list[item]?.ToString() ?? "";
            
            if (itemText.Length > width)
                itemText = itemText.Substring(0, width);

            itemText = itemText.PadRight(width);

            driver.AddStr(itemText);
        }

        public void SetMark(int item, bool value)
        {
            // No marking support implemented
        }

        public IList ToList()
        {
            return (IList)_list;
        }
    }
}