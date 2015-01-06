﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using OpenNLP.Tools.Util.Ling;

namespace OpenNLP.Tools.Util.Trees
{
    /**
 * An individual dependency between a head and a dependent.
 * The head and dependent are represented as a Label.
 * For example, these can be a
 * Word or a WordTag.  If one wishes the dependencies to preserve positions
 * in a sentence, then each can be a LabeledConstituent.
 *
 * @author Christopher Manning
 * @author Spence Green
 * 
 */

    public class UnnamedDependency : Dependency<Label, Label, Object>
    {
        private static readonly long serialVersionUID = -3768440215342256085L;

        // We store the text of the labels separately because it looks like
        // it is possible for an object to request a hash code using itself
        // in a partially reconstructed state when unserializing.  For
        // example, a TreeGraphNode might ask for the hash code of an
        // UnnamedDependency, which then uses an unfilled member of the same
        // TreeGraphNode to get the hash code.  Keeping the text of the
        // labels breaks that possible cycle.
        protected readonly string regentText;
        protected readonly string dependentText;

        private readonly Label regent;
        private readonly Label vDependent;

        public UnnamedDependency(string regent, string dependent)
        {
            if (regent == null || dependent == null)
            {
                throw new ArgumentException("governor or dependent cannot be null");
            }

            var headLabel = new CoreLabel();
            headLabel.SetValue(regent);
            headLabel.SetWord(regent);
            this.regent = headLabel;

            var depLabel = new CoreLabel();
            depLabel.SetValue(dependent);
            depLabel.SetWord(dependent);
            this.vDependent = depLabel;

            regentText = regent;
            dependentText = dependent;
        }

        public UnnamedDependency(Label regent, Label dependent)
        {
            if (regent == null || dependent == null)
            {
                throw new ArgumentException("governor or dependent cannot be null");
            }
            this.regent = regent;
            this.vDependent = dependent;

            regentText = GetText(regent);
            dependentText = GetText(dependent);
        }

        public Label Governor()
        {
            return regent;
        }

        public Label Dependent()
        {
            return vDependent;
        }

        public virtual Object Name()
        {
            return null;
        }

        protected string GetText(Label label)
        {
            if (label is HasWord)
            {
                string word = ((HasWord) label).GetWord();
                if (word != null)
                {
                    return word;
                }
            }
            return label.Value();
        }

        //@Override
        public override int GetHashCode()
        {
            return regentText.GetHashCode() ^ dependentText.GetHashCode();
        }

        //@Override
        public override bool Equals(Object o)
        {
            return EqualsIgnoreName(o);
        }

        public bool EqualsIgnoreName(Object o)
        {
            if (this == o)
            {
                return true;
            }
            else if (!(o is UnnamedDependency))
            {
                return false;
            }
            var d = (UnnamedDependency) o;

            string thisHeadWord = regentText;
            string thisDepWord = dependentText;
            string headWord = d.regentText;
            string depWord = d.dependentText;

            return thisHeadWord.Equals(headWord) && thisDepWord.Equals(depWord);
        }

        //@Override
        public override string ToString()
        {
            return string.Format("{0} --> {1}", regentText, dependentText);
        }

        /**
   * Provide different printing options via a string keyword.
   * The recognized options are currently "xml", and "predicate".
   * Otherwise the default ToString() is used.
   */

        public virtual string ToString(string format)
        {
            switch (format)
            {
                case "xml":
                    return "  <dep>\n    <governor>" + XMLUtils.XmlEscape(Governor().Value()) +
                           "</governor>\n    <dependent>" + XMLUtils.XmlEscape(Dependent().Value()) +
                           "</dependent>\n  </dep>";
                case "predicate":
                    return "dep(" + Governor() + "," + Dependent() + ")";
                default:
                    return ToString();
            }
        }

        public virtual DependencyFactory DependencyFactory()
        {
            return DependencyFactoryHolder.df;
        }

        public static DependencyFactory Factory()
        {
            return DependencyFactoryHolder.df;
        }

        // extra class guarantees correct lazy loading (Bloch p.194)
        private static class DependencyFactoryHolder
        {
            public static readonly DependencyFactory df = new UnnamedDependencyFactory();
        }

        /**
   * A <code>DependencyFactory</code> acts as a factory for creating objects
   * of class <code>Dependency</code>
   */

        private /*static*/ class UnnamedDependencyFactory : DependencyFactory
        {
            /**
     * Create a new <code>Dependency</code>.
     */

            public Dependency<Label, Label, Object> NewDependency(Label regent, Label dependent)
            {
                return NewDependency(regent, dependent, null);
            }

            /**
     * Create a new <code>Dependency</code>.
     */

            public Dependency<Label, Label, Object> NewDependency(Label regent, Label dependent, Object name)
            {
                return new UnnamedDependency(regent, dependent);
            }
        }
    }
}