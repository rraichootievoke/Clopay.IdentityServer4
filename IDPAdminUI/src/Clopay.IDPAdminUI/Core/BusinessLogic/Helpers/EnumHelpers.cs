using System;
using System.Collections.Generic;
using System.Linq;
using Clopay.IDPAdminUI.Core.BusinessLogic.Shared.Dtos.Common;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Helpers
{
	public class EnumHelpers
	{
		public static List<SelectItemDto> ToSelectList<T>() where T : struct, IComparable
		{
			var selectItems = Enum.GetValues(typeof(T))
				.Cast<T>()
				.Select(x => new SelectItemDto(Convert.ToInt16(x).ToString(), x.ToString())).ToList();

			return selectItems;
		}
	}
}