﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenNLP.Tools.Trees
{
    /// <summary>
    /// A base class for a HeadFinder similar to the one described in
    /// Michael Collins' 1999 thesis.  For a given constituent we perform operations
    /// like (this is for "left" or "right":
    /// <pre>
    /// for categoryList in categoryLists
    ///   for index = 1 to n [or n to 1 if R->L]
    ///     for category in categoryList
    ///       if category equals daughter[index] choose it.
    /// </pre>
    /// 
    /// with a readonly default that goes with the direction (L->R or R->L)
    /// For most constituents, there will be only one category in the list,
    /// the exception being, in Collins' original version, NP.
    /// 
    /// It is up to the overriding base class to initialize the map
    /// from constituent type to categoryLists, "nonTerminalInfo",
    /// in its constructor.
    /// Entries are presumed to be of type string[][].  Each string[] is a list of
    /// categories, except for the first entry, which specifies direction of
    /// traversal and must be one of the following:
    /// 
    /// <ul>
    /// <li> "left" means search left-to-right by category and then by position</li>
    /// <li> "leftdis" means search left-to-right by position and then by category</li>
    /// <li> "right" means search right-to-left by category and then by position</li>
    /// <li> "rightdis" means search right-to-left by position and then by category</li>
    /// <li> "leftexcept" means to take the first thing from the left that isn't in the list</li>
    /// <li> "rightexcept" means to take the first thing from the right that isn't on the list</li>
    /// </ul>
    /// 
    /// Changes:
    /// <ul>
    /// <li> 2002/10/28 -- Category label identity checking now uses the
    /// equals() method instead of ==, so not interning category labels
    /// shouldn't break things anymore.  (Roger Levy)</li>
    /// <li> 2003/02/10 -- Changed to use TreebankLanguagePack and to cut on
    /// characters that set off annotations, so this should work even if
    /// functional tags are still on nodes.</li>
    /// <li> 2004/03/30 -- Made abstract base class and subclasses for CollinsHeadFinder,
    /// ModCollinsHeadFinder, SemanticHeadFinder, ChineseHeadFinder
    /// (and trees.icegb.ICEGBHeadFinder, trees.international.negra.NegraHeadFinder,
    /// and movetrees.EnglishPennMaxProjectionHeadFinder)</li>
    /// <li> 2011/01/13 -- Add support for categoriesToAvoid (which can be set to ensure that
    /// punctuation is not the head if there are other options)</li>
    /// </ul>
    /// 
    /// @author Christopher Manning
    /// @author Galen Andrew
    /// 
    /// Code retrieved on the Stanford parser and ported to C# (see http://nlp.stanford.edu/software/lex-parser.shtml)
    /// </summary>
    public abstract class AbstractCollinsHeadFinder : IHeadFinder
    {
        protected readonly AbstractTreebankLanguagePack tlp;
        protected Dictionary<string, string[][]> nonTerminalInfo;

        /// <summary>
        /// Default direction if no rule is found for category (the head/parent).
        /// Subclasses can turn it on if they like.
        /// If they don't it is an error if no rule is defined for a category (null is returned).
        /// </summary>
        protected string[] DefaultRule; // = null;

        /// <summary>
        /// These are built automatically from categoriesToAvoid and used in a fairly
        /// different fashion from defaultRule (above).  These are used for categories
        /// that do have defined rules but where none of them have matched.  Rather
        /// than picking the rightmost or leftmost child, we will use these to pick
        /// the the rightmost or leftmost child which isn't in categoriesToAvoid.
        /// </summary>
        protected string[] DefaultLeftRule;
        protected string[] DefaultRightRule;

        /// <summary>
        /// Construct a HeadFinder.
        /// The TreebankLanguagePack is used to get basic categories. The remaining arguments
        /// set categories which, if it comes to last resort processing (i.e., none of
        /// the rules matched), will be avoided as heads. In last resort processing,
        /// it will attempt to match the leftmost or rightmost constituent not in this
        /// set but will fall back to the left or rightmost constituent if necessary.
        /// </summary>
        /// <param name="tlp">TreebankLanguagePack used to determine basic category</param>
        /// <param name="categoriesToAvoid">Constituent types to avoid as head</param>
        protected AbstractCollinsHeadFinder(AbstractTreebankLanguagePack tlp, string[] categoriesToAvoid)
        {
            this.tlp = tlp;
            // automatically build defaultLeftRule, defaultRightRule
            DefaultLeftRule = new string[categoriesToAvoid.Length + 1];
            DefaultRightRule = new string[categoriesToAvoid.Length + 1];
            if (categoriesToAvoid.Length > 0)
            {
                DefaultLeftRule[0] = "leftexcept";
                DefaultRightRule[0] = "rightexcept";
                Array.Copy(categoriesToAvoid, 0, DefaultLeftRule, 1, categoriesToAvoid.Length);
                Array.Copy(categoriesToAvoid, 0, DefaultRightRule, 1, categoriesToAvoid.Length);
            }
            else
            {
                DefaultLeftRule[0] = "left";
                DefaultRightRule[0] = "right";
            }
        }

        /// <summary>
        /// Generally will be false, except for SemanticHeadFinder
        /// </summary>
        public virtual bool MakesCopulaHead()
        {
            return false;
        }

        /// <summary>
        /// A way for subclasses for corpora with explicit head markings
        /// to return the explicitly marked head
        /// </summary>
        /// <param name="t">a tree to find the head of</param>
        /// <returns>the marked head-- null if no marked head</returns>
        protected Tree FindMarkedHead(Tree t)
        {
            return null;
        }

        /// <summary>
        /// Determine which daughter of the current parse tree is the head.
        /// </summary>
        /// <param name="t">
        /// The parse tree to examine the daughters of.
        /// If this is a leaf, <code>null</code> is returned
        /// </param>
        /// <returns>
        /// The daughter parse tree that is the head of <code>t</code>
        /// </returns>
        public Tree DetermineHead(Tree t)
        {
            return DetermineHead(t, null);
        }

        /// <summary>
        /// Determine which daughter of the current parse tree is the head.
        /// </summary>
        /// <param name="t">
        /// The parse tree to examine the daughters of. 
        /// If this is a leaf, <code>null</code> is returned
        /// </param>
        /// <param name="parent">The parent of t</param>
        /// <returns>
        /// The daughter parse tree that is the head of <code>t</code>.
        /// Returns null for leaf nodes.
        /// </returns>
        public Tree DetermineHead(Tree t, Tree parent)
        {
            if (nonTerminalInfo == null)
            {
                throw new InvalidDataException(
                    "Classes derived from AbstractCollinsHeadFinder must create and fill HashMap nonTerminalInfo.");
            }
            if (t == null || t.IsLeaf())
            {
                throw new ArgumentException("Can't return head of null or leaf Tree.");
            }

            Tree[] kids = t.Children();

            Tree theHead;
            // first check if subclass found explicitly marked head
            if ((theHead = FindMarkedHead(t)) != null)
            {
                return theHead;
            }

            // if the node is a unary, then that kid must be the head
            // it used to special case preterminal and ROOT/TOP case
            // but that seemed bad (especially hardcoding string "ROOT")
            if (kids.Length == 1)
            {
                return kids[0];
            }

            return DetermineNonTrivialHead(t, parent);
        }

        /// <summary>
        /// Called by determineHead and may be overridden in subclasses
        /// if special treatment is necessary for particular categories.
        /// </summary>
        /// <param name="t">The tre to determine the head daughter of</param>
        /// <param name="parent">The parent of t (or may be null)</param>
        /// <returns>The head daughter of t</returns>
        protected virtual Tree DetermineNonTrivialHead(Tree t, Tree parent)
        {
            Tree theHead = null;
            string motherCat = tlp.BasicCategory(t.Label().Value());
            if (motherCat.StartsWith("@"))
            {
                motherCat = motherCat.Substring(1);
            }
            // We know we have nonterminals underneath
            // (a bit of a Penn Treebank assumption, but).

            // Look at label.
            // a total special case....
            // first look for POS tag at end
            // this appears to be redundant in the Collins case since the rule already would do that
            //    Tree lastDtr = t.lastChild();
            //    if (tlp.basicCategory(lastDtr.label().value()).equals("POS")) {
            //      theHead = lastDtr;
            //    } else {
            string[][] how = null;
            var success = nonTerminalInfo.TryGetValue(motherCat, out how);
            Tree[] kids = t.Children();
            if (!success)
            {
                if (DefaultRule != null)
                {
                    return TraverseLocate(kids, DefaultRule, true);
                }
                else
                {
                    throw new ArgumentException("No head rule defined for " + motherCat + " using "
                        /*+ this.getClass()*/+ " in " + t);
                }
            }
            for (int i = 0; i < how.Length; i++)
            {
                bool lastResort = (i == how.Length - 1);
                theHead = TraverseLocate(kids, how[i], lastResort);
                if (theHead != null)
                {
                    break;
                }
            }
            return theHead;
        }

        /// <summary>
        /// Attempt to locate head daughter tree from among daughters.
        /// Go through daughterTrees looking for things from or not in a set given by
        /// the contents of the array how, and if
        /// you do not find one, take leftmost or rightmost perhaps matching thing iff
        /// lastResort is true, otherwise return <code>null</code>.
        /// </summary>
        protected Tree TraverseLocate(Tree[] daughterTrees, string[] how, bool lastResort)
        {
            int headIdx;
            switch (how[0])
            {
                case "left":
                    headIdx = FindLeftHead(daughterTrees, how);
                    break;
                case "leftdis":
                    headIdx = FindLeftDisHead(daughterTrees, how);
                    break;
                case "leftexcept":
                    headIdx = FindLeftExceptHead(daughterTrees, how);
                    break;
                case "right":
                    headIdx = FindRightHead(daughterTrees, how);
                    break;
                case "rightdis":
                    headIdx = FindRightDisHead(daughterTrees, how);
                    break;
                case "rightexcept":
                    headIdx = FindRightExceptHead(daughterTrees, how);
                    break;
                default:
                    throw new InvalidEnumArgumentException("ERROR: invalid direction type " + how[0] +
                                                           " to nonTerminalInfo map in AbstractCollinsHeadFinder.");
            }

            // what happens if our rule didn't match anything
            if (headIdx < 0)
            {
                if (lastResort)
                {
                    // use the default rule to try to match anything except categoriesToAvoid
                    // if that doesn't match, we'll return the left or rightmost child (by
                    // setting headIdx).  We want to be careful to ensure that postOperationFix
                    // runs exactly once.
                    string[] rule;
                    if (how[0].StartsWith("left"))
                    {
                        headIdx = 0;
                        rule = DefaultLeftRule;
                    }
                    else
                    {
                        headIdx = daughterTrees.Length - 1;
                        rule = DefaultRightRule;
                    }
                    Tree child = TraverseLocate(daughterTrees, rule, false);
                    if (child != null)
                    {
                        return child;
                    }
                    else
                    {
                        return daughterTrees[headIdx];
                    }
                }
                else
                {
                    // if we're not the last resort, we can return null to let the next rule try to match
                    return null;
                }
            }

            headIdx = PostOperationFix(headIdx, daughterTrees);

            return daughterTrees[headIdx];
        }

        private int FindLeftHead(Tree[] daughterTrees, string[] how)
        {
            for (int i = 1; i < how.Length; i++)
            {
                for (int headIdx = 0; headIdx < daughterTrees.Length; headIdx++)
                {
                    string childCat = tlp.BasicCategory(daughterTrees[headIdx].Label().Value());
                    if (how[i].Equals(childCat))
                    {
                        return headIdx;
                    }
                }
            }
            return -1;
        }

        private int FindLeftDisHead(Tree[] daughterTrees, string[] how)
        {
            for (int headIdx = 0; headIdx < daughterTrees.Length; headIdx++)
            {
                string childCat = tlp.BasicCategory(daughterTrees[headIdx].Label().Value());
                for (int i = 1; i < how.Length; i++)
                {
                    if (how[i].Equals(childCat))
                    {
                        return headIdx;
                    }
                }
            }
            return -1;
        }

        private int FindLeftExceptHead(Tree[] daughterTrees, string[] how)
        {
            for (int headIdx = 0; headIdx < daughterTrees.Length; headIdx++)
            {
                string childCat = tlp.BasicCategory(daughterTrees[headIdx].Label().Value());
                bool found = true;
                for (int i = 1; i < how.Length; i++)
                {
                    if (how[i].Equals(childCat))
                    {
                        found = false;
                    }
                }
                if (found)
                {
                    return headIdx;
                }
            }
            return -1;
        }

        private int FindRightHead(Tree[] daughterTrees, string[] how)
        {
            for (int i = 1; i < how.Length; i++)
            {
                for (int headIdx = daughterTrees.Length - 1; headIdx >= 0; headIdx--)
                {
                    string childCat = tlp.BasicCategory(daughterTrees[headIdx].Label().Value());
                    if (how[i].Equals(childCat))
                    {
                        return headIdx;
                    }
                }
            }
            return -1;
        }

        // from right, but search for any of the categories, not by category in turn
        private int FindRightDisHead(Tree[] daughterTrees, string[] how)
        {
            for (int headIdx = daughterTrees.Length - 1; headIdx >= 0; headIdx--)
            {
                string childCat = tlp.BasicCategory(daughterTrees[headIdx].Label().Value());
                for (int i = 1; i < how.Length; i++)
                {
                    if (how[i].Equals(childCat))
                    {
                        return headIdx;
                    }
                }
            }
            return -1;
        }

        private int FindRightExceptHead(Tree[] daughterTrees, string[] how)
        {
            for (int headIdx = daughterTrees.Length - 1; headIdx >= 0; headIdx--)
            {
                string childCat = tlp.BasicCategory(daughterTrees[headIdx].Label().Value());
                bool found = true;
                for (int i = 1; i < how.Length; i++)
                {
                    if (how[i].Equals(childCat))
                    {
                        found = false;
                    }
                }
                if (found)
                {
                    return headIdx;
                }
            }
            return -1;
        }

        /// <summary>
        /// A way for subclasses to fix any heads under special conditions.
        /// The default does nothing.
        /// </summary>
        /// <param name="headIdx">The index of the proposed head</param>
        /// <param name="daughterTrees">The array of daughter trees</param>
        /// <returns>The new headIndex</returns>
        protected virtual int PostOperationFix(int headIdx, Tree[] daughterTrees)
        {
            return headIdx;
        }
    }
}