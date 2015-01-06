﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenNLP.Tools.Util.Trees.TRegex.Tsurgeon
{
    /** Adjoin in a tree (like in TAG).
 *
 *  @author Roger Levy (rog@nlp.stanford.edu)
 */

    public class AdjoinNode : TsurgeonPattern
    {
        private readonly AuxiliaryTree padjunctionTree;

        public AdjoinNode(AuxiliaryTree t, TsurgeonPattern p) :
            this("adjoin", t, p)
        {
        }

        public AdjoinNode(string name, AuxiliaryTree t, TsurgeonPattern p) :
            base(name, new TsurgeonPattern[] {p})
        {
            if (t == null || p == null)
            {
                throw new NullReferenceException("AdjoinNode: illegal null argument, t=" + t + ", p=" + p);
            }
            padjunctionTree = t;
        }

        protected AuxiliaryTree adjunctionTree()
        {
            return padjunctionTree;
        }

        //@Override
        public override TsurgeonMatcher matcher(Dictionary<string, Tree> newNodeNames, CoindexationGenerator coindexer)
        {
            return new Matcher(newNodeNames, coindexer, this);
        }

        private class Matcher : TsurgeonMatcher
        {
            private AdjoinNode node;

            public Matcher(Dictionary<string, Tree> newNodeNames, CoindexationGenerator coindexer, AdjoinNode node) :
                base(node, newNodeNames, coindexer)
            {
                this.node = node;
            }

            //@Override
            public override Tree evaluate(Tree tree, TregexMatcher tregex)
            {
                // find match and get its parent
                Tree targetNode = childMatcher[0].evaluate(tree, tregex);
                Tree parent = targetNode.Parent(tree);
                // put children underneath target in foot of auxilary tree
                AuxiliaryTree ft = node.padjunctionTree.copy(this);
                ft.foot.SetChildren(targetNode.GetChildrenAsList());
                // replace match with root of auxiliary tree
                if (parent == null)
                {
                    return ft.tree;
                }
                else
                {
                    int i = parent.ObjectIndexOf(targetNode);
                    parent.SetChild(i, ft.tree);
                    return tree;
                }
            }
        }

        //@Override
        public override string ToString()
        {
            return base.ToString() + "<-" + padjunctionTree.ToString();
        }
    }
}