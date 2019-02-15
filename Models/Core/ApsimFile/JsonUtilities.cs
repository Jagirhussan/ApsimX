﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Models.Core.ApsimFile
{
    /// <summary>
    /// A collection of json utilities.
    /// </summary>
    /// <remarks>
    /// If you write a new utility for this class, please write a unit test
    /// for it. See JsonUtilitiesTests.cs in the UnitTests project.
    /// </remarks>
    public static class JsonUtilities
    {
        /// <summary>
        /// Returns the name of a node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <remarks>
        /// This actually fetches the 'Apsim' name of the node.
        /// e.g. For a Report called HarvestReport this will return 
        /// HarvestReport.
        /// </remarks>
        public static string Name(JToken node)
        {
            if (node == null)
                return null;

            if (node is JObject)
            {
                JProperty nameProperty = (node as JObject).Property("Name");
                if (nameProperty == null)
                    throw new Exception(string.Format("Attempted to fetch the name property of json node {0}.", node.ToString()));
                return (string)nameProperty.Value;
            }

            if (node is JProperty)
                return (node as JProperty).Name;

            return string.Empty;
        }

        /// <summary>
        /// Returns the type of an apsim model node.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="withNamespace">
        /// If true, the namespace will be included in the returned type name.
        /// e.g. Models.Core.Simulations
        /// </param>
        public static string Type(JToken node, bool withNamespace = false)
        {
            // If the node is not a JObject, it is not an apsim model.
            if ( !(node is JObject) )
                return null;

            JProperty typeProperty = (node as JObject).Property("$type");

            if (typeProperty == null)
                return null;

            string typeName = (string)typeProperty.Value;

            // Type is written as "Namespace.TypeName, Assembly"
            // e.g. Models.Core.Simulations, Models
            int indexOfComma = typeName.IndexOf(',');
            if (indexOfComma >= 0)
                typeName = typeName.Substring(0, indexOfComma);

            if (!withNamespace)
            {
                int indexOfLastPeriod = typeName.LastIndexOf('.');
                if (indexOfLastPeriod >= 0)
                    typeName = typeName.Substring(indexOfLastPeriod + 1);
            }

            return typeName;
        }

        /// <summary>
        /// Returns the child models of a given node.
        /// Will never return null.
        /// </summary>
        /// <param name="node">The node.</param>
        public static List<JObject> Children(JObject node)
        {
            if (node == null)
                return new List<JObject>();

            JProperty childrenProperty = node.Property("Children");

            if (childrenProperty == null)
                return new List<JObject>();

            IEnumerable<JToken> children = childrenProperty.Values();

            if (children == null)
                return new List<JObject>();

            return children.Cast<JObject>().ToList();
        }

        /// <summary>
        /// Returns the child models of a given node that have the specified type.
        /// Will never return null.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="typeName">The type of children to return.</param>
        public static List<JObject> ChildrenOfType(JObject node, string typeName)
        {
            return ChildrenRecursively(node).Where(child => Type(child) == typeName).ToList();
        }

        /// <summary>
        /// Returns the first child model of a given node that has the specified name.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="name">The type of children to return.</param>
        /// <returns>The found child or null if not found.</returns>
        public static JObject ChildWithName(JObject node, string name)
        {
            return Children(node).Find(child => Name(child) == name);
        }

        /// <summary>
        /// Returns all descendants of a given node.
        /// Will never return null.
        /// </summary>
        /// <param name="node">The node.</param>
        public static List<JObject> ChildrenRecursively(JObject node)
        {
            List<JObject> descendants = new List<JObject>();
            Descendants(node, ref descendants);
            return descendants;
        }

        /// <summary>
        /// Returns a all descendants of a node, which are of a given type.
        /// Will never return null;
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="typeFilter">Type type name, with or without the namespace.</param>
        /// <returns></returns>
        public static List<JObject> ChildrenRecursively(JObject node, string typeFilter)
        {
            List<JObject> descendants = new List<JObject>();
            Descendants(node, ref descendants, typeFilter);
            return descendants;
        }

        /// <summary>
        /// Perform a search and replace in manager script.
        /// </summary>
        /// <param name="manager">The manager model.</param>
        /// <param name="searchPattern">The string to search for.</param>
        /// <param name="replacePattern">The string to replace.</param>
        public static void ReplaceManagerCode(JObject manager, string searchPattern, string replacePattern)
        {
            string code = manager["Code"]?.ToString();
            if (code == null || searchPattern == null)
                return;
            manager["Code"] = code.Replace(searchPattern, replacePattern);
        }

        /// <summary>
        /// Perform a search and replace in manager script.
        /// </summary>
        /// <param name="manager">The manager model.</param>
        /// <param name="searchPattern">The pattern to search for.</param>
        /// <param name="replacePattern">The string to replace.</param>
        /// <param name="options">Regular expression options to use. Default value is none.</param>
        public static void ReplaceManagerCodeUsingRegex(JObject manager, string searchPattern, string replacePattern, RegexOptions options = RegexOptions.None)
        {
            string code = manager["Code"]?.ToString();
            if (code == null || searchPattern == null)
                return;
            manager["Code"] = Regex.Replace(code, searchPattern, replacePattern, options);
        }

        /// <summary>
        /// Return the parent APSIM model token for the specified model token.
        /// </summary>
        /// <param name="modelToken">The model token to find the parent for.</param>
        /// <returns>The parent or null if not found.</returns>
        public static JToken Parent(JToken modelToken)
        {
            var obj = modelToken.Parent;
            while (obj != null)
            {
                if (Type(obj) != null)
                    return obj;

                obj = obj.Parent;
            }

            return null;
        }

        /// <summary>
        /// Rename a child property if it exists.
        /// </summary>
        /// <param name="modelToken">The APSIM model token.</param>
        /// <param name="propertyName">The name of the property to rename.</param>
        /// <param name="newPropertyName">The new name of the property.</param>
        public static void RenameProperty(JToken modelToken, string propertyName, string newPropertyName)
        {
            var valueToken = modelToken[propertyName];
            if (valueToken != null && valueToken.Parent is JProperty)
            {
                var propertyToken = valueToken.Parent as JProperty;
                propertyToken.Remove(); // remove from parent.
                modelToken[newPropertyName] = valueToken;
            }
        }

        /// <summary>
        /// Gets a list of property values.
        /// </summary>
        /// <param name="node">The model node to look under.</param>
        /// <param name="propertyName">The property name to return.</param>
        /// <returns>The values or null if not found.</returns>
        public static List<string> Values(JObject node, string propertyName)
        {
            var variableNamesObject = node[propertyName];
            if (variableNamesObject is JArray)
            {
                var array = variableNamesObject as JArray;
                return array.Values<string>().ToList();
            }
            return null;
        }

        /// <summary>
        /// Sets a list of property values.
        /// </summary>
        /// <param name="node">The model node to look under.</param>
        /// <param name="propertyName">The property name to return.</param>
        /// <param name="values">New values</param>
        /// <returns>The values or null if not found.</returns>
        public static void SetValues<T>(JObject node, string propertyName, IEnumerable<T> values)
        {
            var variableNamesObject = node[propertyName];
            if (variableNamesObject == null)
            {
                variableNamesObject = new JArray();
                node[propertyName] = variableNamesObject;
            }
            if (variableNamesObject is JArray)
            {
                var array = variableNamesObject as JArray;
                array.Clear();
                foreach (var value in values)
                    array.Add(value);
            }
        }

        /// <summary>
        /// Add a constant function to the specified JSON model token.
        /// </summary>
        /// <param name="modelToken">The APSIM model token.</param>
        /// <param name="name">The name of the constant function</param>
        /// <param name="fixedValue">The fixed value of the constant function</param>
        public static void AddConstantFunctionIfNotExists(JObject modelToken, string name, string fixedValue)
        {
            if (ChildWithName(modelToken, name) == null)
            {
                JArray children = modelToken["Children"] as JArray;
                if (children == null)
                {
                    children = new JArray();
                    modelToken["Children"] = children;
                }

                JObject constantModel = new JObject();
                constantModel["$type"] = "Models.Functions.Constant, Models";
                constantModel["Name"] = name;
                constantModel["FixedValue"] = fixedValue;
                children.Add(constantModel);
            }
        }

        /// <summary>
        /// Helper method for <see cref="ChildrenRecursively(JObject)"/>.
        /// Will never return null.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="descendants">List of descendants.</param>
        /// <param name="typeFilter">Type name by which to filter.</param>
        private static void Descendants(JObject node, ref List<JObject> descendants, string typeFilter = null)
        {
            if (node == null)
                return;

            List<JObject> children = Children(node);
            if (children == null)
                return;

            if (descendants == null)
                descendants = new List<JObject>();

            foreach (JObject child in children)
            {
                if (string.IsNullOrEmpty(typeFilter) || Type(child, typeFilter.Contains('.')) == typeFilter)
                    descendants.Add(child);
                Descendants(child, ref descendants, typeFilter);
            }
        }

    }
}