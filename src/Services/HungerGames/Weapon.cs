using System;

namespace App.HungerGames {
	public enum Weapon {
		none, 
		stick, 
		knife,
		sword,
		gun
	}

	public static class WeaponExtensions {
		public static string GetName(this Weapon weaponType) {
			switch (weaponType) {
				case Weapon.stick:
					return "pedaço de pau";
				case Weapon.knife:
					return "faca";
				case Weapon.sword:
					return "espada";
				case Weapon.gun:
					return "arma";
				default:
					throw new ArgumentOutOfRangeException(nameof(weaponType), weaponType, null);
			}
		}
	}

}
