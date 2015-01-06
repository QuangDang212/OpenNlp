﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenNLP.Tools.Util.Ling;

namespace OpenNLP.Tools.Util.Trees
{
    /**
 * Normalizes trees in the way used in Manning and Carpenter 1997.
 * NB: This implementation is still incomplete!
 * The normalizations performed are: (i) terminals are interned, (ii)
 * nonterminals are stripped of alternants, functional tags and
 * cross-reference codes, and then interned, (iii) empty
 * elements (ones with nonterminal label "-NONE-") are deleted from the
 * tree, (iv) the null label at the root node is replaced with the label
 * "ROOT". <br>
 * 17 Apr 2001: This was fixed to work with different kinds of labels,
 * by making proper use of the Label interface, after it was moved into
 * the trees module.
 * <p/>
 * The normalizations of the original (Prolog) BobChrisNormalize were:
 * 1. Remap the root node to be called 'ROOT'
 * 2. Truncate all nonterminal labels before characters introducing
 * annotations according to TreebankLanguagePack
 * (traditionally, -, =, | or # (last for BLLIP))
 * 3. Remap the representation of certain leaf symbols (brackets etc.)
 * 4. Map to lowercase all leaf nodes
 * 5. Delete empty/trace nodes (ones marked '-NONE-')
 * 6. Recursively delete any nodes that do not dominate any words
 * 7. Delete A over A nodes where the top A dominates nothing else
 * 8. Remove backslahes from lexical items
 * (the Treebank inserts them to escape slashes (/) and stars (*)).
 * 4 is deliberately omitted, and a few things are purely aesthetic.
 * <p/>
 * 14 June 2002: It now deletes unary A over A if both nodes' labels are equal
 * (7), and (6) was always part of the Tree.prune() functionality...
 * 30 June 2005: Also splice out an EDITED node, just in case you're parsing
 * the Brown corpus.
 *
 * @author Christopher Manning
 */

    public class BobChrisTreeNormalizer : TreeNormalizer, TreeTransformer
    {
        protected readonly TreebankLanguagePack tlp;


        public BobChrisTreeNormalizer() :
            this(new PennTreebankLanguagePack())
        {
        }

        public BobChrisTreeNormalizer(TreebankLanguagePack tlp)
        {
            this.tlp = tlp;
        }


        /**
   * Normalizes a leaf contents.
   * This implementation interns the leaf.
   */
        //@Override
        public override string NormalizeTerminal(string leaf)
        {
            // We could unquote * and / with backslash \ in front of them
            return leaf /*.intern()*/;
        }


        /**
   * Normalizes a nonterminal contents.
   * This implementation strips functional tags, etc. and interns the
   * nonterminal.
   */
        //@Override
        public override string NormalizeNonterminal(string category)
        {
            return cleanUpLabel(category) /*.intern()*/;
        }


        /**
   * Remove things like hyphened functional tags and equals from the
   * end of a node label.  This version always just returns the phrase
   * structure category, or "ROOT" if the label was <code>null</code>.
   *
   * @param label The label from the treebank
   * @return The cleaned up label (phrase structure category)
   */

        protected string cleanUpLabel( /*readonly */ string label)
        {
            if (label == null || label.Length == 0)
            {
                return "ROOT";
                // string constants are always interned
            }
            else
            {
                return tlp.BasicCategory(label);
            }
        }


        /**
   * Normalize a whole tree -- one can assume that this is the
   * root.  This implementation deletes empty elements (ones with
   * nonterminal tag label '-NONE-') from the tree, and splices out
   * unary A over A nodes.  It does work for a null tree.
   */
        //@Override
        public override Tree NormalizeWholeTree(Tree tree, TreeFactory tf)
        {
            return tree.Prune(emptyFilter.test, tf).SpliceOut(aOverAFilter.test, tf);
        }

        //@Override
        public Tree TransformTree(Tree tree)
        {
            return NormalizeWholeTree(tree, tree.TreeFactory());
        }


        protected EmptyFilter emptyFilter = new EmptyFilter();

        protected AOverAFilter aOverAFilter = new AOverAFilter();

        private static readonly long serialVersionUID = -1005188028979810143L;


        public /*static */ class EmptyFilter /*: Predicate<Tree>*/ /*, Serializable*/
        {

            private static readonly long serialVersionUID = 8914098359495987617L;

            /** Doesn't accept nodes that only cover an empty. */

            public bool test(Tree t)
            {
                Tree[] kids = t.Children();
                Label l = t.Label();
                // Delete (return false for) empty/trace nodes (ones marked '-NONE-')
                return
                    ! ((l != null) && "-NONE-".Equals(l.Value()) && !t.IsLeaf() && kids.Length == 1 && kids[0].IsLeaf());
            }

            //    private static readonly long serialVersionUID = 1L;

        } // end class EmptyFilter


        public /*static*/ class AOverAFilter /* : Predicate<Tree>*/ /*, Serializable*/
        {

            /** Doesn't accept nodes that are A over A nodes (perhaps due to
     *  empty removal or are EDITED nodes).
     */

            public bool test(Tree t)
            {
                if (t.IsLeaf() || t.IsPreTerminal())
                {
                    return true;
                }
                // The special switchboard non-terminals clause
                if ("EDITED".Equals(t.Label().Value()) || "CODE".Equals(t.Label().Value()))
                {
                    return false;
                }
                if (t.NumChildren() != 1)
                {
                    return true;
                }
                return
                    ! (t.Label() != null && t.Label().Value() != null &&
                       t.Label().Value().Equals(t.GetChild(0).Label().Value()));
            }

            private static readonly long serialVersionUID = 1L;

        } // end class AOverAFilter
    }
}