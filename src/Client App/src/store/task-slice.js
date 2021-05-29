import { createSlice, current } from '@reduxjs/toolkit';

const initialState = {
    isLoading: false,    
    taskInfo: {}
}

const taskSlice = createSlice({

    name: 'task',
    initialState: initialState,
    reducers: {
        start_loading: (state) => {
            return {
                ...state,
                isLoading: true
            }
        },
        load_task_success: (state, action) => {

            return {
                ...state,
                isLoading: false,
                taskInfo: { data: action.payload.data, count: action.payload.totalRecords }                
            }
        },
        add_task_success: (state) => {
            return {
                ...state,
                isLoading: false                
            }
        },
        allocate_task_success: (state) => {
            return {
                ...state,
                isLoading: false                
            }
        },
        update_task_sucess: (state, action) => {

            var task = current(state.taskInfo.data).find(item => item.id === action.payload.data.id);
            const index = current(state.taskInfo.data).indexOf(task);

            var temp = [...current(state.taskInfo.data)]
            console.log(action.payload.data)
            console.log(temp)
            temp.splice(index, 1, action.payload.data);

            var updatedState = {
                
                ...state.taskInfo,
                data: temp,

            }

            return {
                ...state,
                isLoading: false,
                taskInfo: updatedState

            }
        },
        error: (state) => {
            return {
                ...state,
                isLoading: false
            }
        },
        reset: () => initialState
    }

});

export const taskActions = taskSlice.actions;

export default taskSlice.reducer;