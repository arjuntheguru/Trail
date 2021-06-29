import { createSlice, current } from "@reduxjs/toolkit";
import { idle, loading, succeeded, failed } from "../utils/stateStatus";

const initialState = {
	status: idle,
	rootData: {},
	userInfo: {},
};

const userSlice = createSlice({
	name: "user",
	initialState: initialState,
	reducers: {
		start_loading: (state) => {
			return {
				...state,
				status: loading,
			};
		},
		load_user_success: (state, action) => {
			return {
				...state,
				status: succeeded,
				rootData: action.payload,
				userInfo: action.payload,
			};
		},
		register_user_success: (state) => {
			return {
				...state,
				status: succeeded,
			};
		},
		update_user_success: (state, action) => {
			var user = current(state.rootData).find(
				(item) => item.id === action.payload.data.id
			);
			const index = current(state.rootData).indexOf(user);

			var temp = [...current(state.rootData)];
			console.log(action.payload.data);
			console.log(temp);
			temp.splice(index, 1, action.payload.data);

			return {
				...state,
				status: succeeded,
				rootData: temp,
				userInfo: temp,
			};
		},
		update_user_password_success: (state) => {
			return {
				...state,
				status: succeeded,
			};
		},
		filter_user_success: (state, action) => {
			var userInfo = state.rootData.filter(
				(item) =>
					item.firstName.toLowerCase().includes(action.payload) ||
					item.lastName.toLowerCase().includes(action.payload) ||
					item.userName.toLowerCase().includes(action.payload) ||
					item.email.toLowerCase().includes(action.payload) ||
					item.role.toLowerCase().includes(action.payload)
			);

			return {
				...state,
				status: succeeded,
				userInfo: userInfo,
			};
		},
		error: (state) => {
			return {
				...state,
				status: failed,
			};
		},
		reset: () => initialState,
	},
});

export const userActions = userSlice.actions;

export default userSlice.reducer;
