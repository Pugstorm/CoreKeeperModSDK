using System;
using System.Collections.Generic;
using Unity.Entities.UI;
using UnityEditor;

namespace Unity.Entities.UI
{
    /// <summary>
    /// Provides entry points into displaying arbitrary content in editor windows or in the inspector.
    /// </summary>
    public static class SelectionUtility
    {
        /// <summary>
        /// Sets the provided content as the global active object.
        /// </summary>
        /// <param name="content">The content to display.</param>
        /// <exception cref="ArgumentNullException">The content cannot be <see langword="null"/>.</exception>
        /// <typeparam name="T">The typeof the content. Must be default constructable.</typeparam>
        public static void ShowInInspector<T>(T content)
            where T : ContentProvider, new()
        {
            ShowInInspector(content, InspectorContentParameters.Default);
        }

        /// <summary>
        /// Sets the provided content as the global active object.
        /// </summary>
        /// <param name="content">The content to display.</param>
        /// <param name="parameters"></param>
        /// <exception cref="ArgumentNullException">The content cannot be <see langword="null"/>.</exception>
        /// <typeparam name="T">The typeof the content. Must be default constructable.</typeparam>
        public static void ShowInInspector<T>(T content, InspectorContentParameters parameters)
            where T : ContentProvider, new()
        {
            if (EqualityComparer<T>.Default.Equals(content, default))
                throw new ArgumentNullException(nameof(content));
            InspectorContent.Show(content, parameters);
        }

        /// <summary>
        /// Sets the provided <see cref="UnityEngine.Object"/> as the global active object.
        /// </summary>
        /// <param name="content">The <see cref="UnityEngine.Object"/> to display.</param>
        /// <exception cref="ArgumentNullException">The content cannot be <see langword="null"/>.</exception>
        public static void ShowInInspector(UnityEngine.Object content)
        {
            if (null == content || !content)
                throw new ArgumentNullException(nameof(content));
            InspectorContent.Show(new UnityObjectContentProvider{ Object = content }, new InspectorContentParameters());
        }

        /// <summary>
        /// Opens a new window and display the provided content.
        /// </summary>
        /// <param name="content">The content to display.</param>
        /// <exception cref="ArgumentNullException">The content cannot be <see langword="null"/>.</exception>
        /// <typeparam name="T">The typeof the content. Must be default constructable.</typeparam>
        public static void ShowInWindow<T>(T content)
            where T : ContentProvider, new()
        {
            ShowInWindow(content, ContentWindowParameters.Default);
        }

        /// <summary>
        /// Opens a new window and display the provided content.
        /// </summary>
        /// <param name="content">The content to display.</param>
        /// <param name="options">The options used to configure the host <see cref="UnityEditor.EditorWindow"/></param>
        /// <exception cref="ArgumentNullException">The content cannot be <see langword="null"/>.</exception>
        /// <typeparam name="T">The typeof the content. Must be default constructable.</typeparam>
        /// <returns>Returns the new window</returns>
        public static EditorWindow ShowInWindow<T>(T content, ContentWindowParameters options)
            where T : ContentProvider, new()
        {
            var wnd = CreateWindow(content, options);
            wnd.Show();
            return wnd;
        }

        public static EditorWindow CreateWindow<T>(T content, ContentWindowParameters options)
            where T : ContentProvider, new()
        {
            if (EqualityComparer<T>.Default.Equals(content, default))
                throw new ArgumentNullException(nameof(content));

            return ContentWindow.Create(content, options);
        }

        /// <summary>
        /// Opens a new window and display the provided <see cref="UnityEngine.Object"/>.
        /// </summary>
        /// <param name="content">The <see cref="UnityEngine.Object"/> to display.</param>
        /// <exception cref="ArgumentNullException">The content cannot be <see langword="null"/>.</exception>
        /// <returns>Returns the new window</returns>
        public static EditorWindow ShowInWindow(UnityEngine.Object content)
        {
            return ShowInWindow(content, ContentWindowParameters.Default);
        }

        /// <summary>
        /// Opens a new window and display the provided <see cref="UnityEngine.Object"/>.
        /// </summary>
        /// <param name="content">The <see cref="UnityEngine.Object"/> to display.</param>
        /// <param name="options">The options used to configure the host <see cref="UnityEditor.EditorWindow"/></param>
        /// <exception cref="ArgumentNullException">The content cannot be <see langword="null"/>.</exception>
        /// <returns>Returns the new window</returns>
        public static EditorWindow ShowInWindow(UnityEngine.Object content, ContentWindowParameters options)
        {
            if (null == content || !content)
                throw new ArgumentNullException(nameof(content));
            return ContentWindow.Show(new UnityObjectContentProvider{ Object = content }, options);
        }
    }
}
