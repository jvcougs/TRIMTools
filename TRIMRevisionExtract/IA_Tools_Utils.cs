using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TRIMRevisionExtract
{
    internal partial class IA_Tools
    {
        class Node
        {
            public List<Node> Nodes = null;
            public string Number = String.Empty;
            public int num_nodes = 0;
            public Node(string number, int number_of_nodes)
            {
                Number = number;
                num_nodes = number_of_nodes;
            }

            void Add(Node n)
            {
                if (Nodes == null)
                {
                    Nodes = new List<Node>();
                }
                Nodes.Add(n);
            }

            StringBuilder Dump()
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("{}");
                return sb;
            }
        };

    }
}
