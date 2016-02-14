using System;
using System.Collections.Generic;

namespace IPADITable
{
    public enum TransactionStates
    {
        UNDEFINED,
        INITIATED,
        TENTATIVELYCOMMITED,
        COMMITED,
        ABORTED
    }
}
